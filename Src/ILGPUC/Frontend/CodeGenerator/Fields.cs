// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Helper function to compute field reference for the specified field of a type.
    /// </summary>
    /// <param name="type">The type node.</param>
    /// <param name="field">The field.</param>
    /// <returns>The target field span.</returns>
    private FieldSpan ComputeFieldSpan(TypeValue type, FieldInfo field)
    {
        var typeInfo = ModuleBuilder.GetTypeInfo(field.FieldType);
        var parentInfo = ModuleBuilder.GetTypeInfo(field.DeclaringType.AsNotNull());
        var fieldIndex = parentInfo.GetAbsoluteIndex(field);

        if (type is StructureType structureType)
            fieldIndex = structureType.RemapFieldIndex(fieldIndex);
        return new(fieldIndex, typeInfo.NumFlattenedFields);
    }

    /// <summary>
    /// Returns true if the given source of a field load is an address: an object
    /// reference or the address of a value type, not the value type itself. A value
    /// type whose only field is a pointer or reference has the IR type of that
    /// pointer, so its values are pointers, too, which point to the field's element
    /// type and never to the value type as its addresses do.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="source">The source of the field load.</param>
    /// <returns>True if the field is loaded from the source address.</returns>
    private bool IsFieldLoadAddress(FieldInfo field, Value source)
    {
        if (source.Type is not PointerType sourceType)
            return false;
        var declaringType = field.DeclaringType.AsNotNull();
        return !declaringType.IsValueType ||
            ModuleBuilder.CreateType(declaringType) is not PointerType valueType ||
            !valueType.ElementType.Equals(sourceType.ElementType);
    }

    /// <summary>
    /// Returns true if a field of the given type has an opaque pointer type in its
    /// structure type: structure types cannot contain themselves, so the fields
    /// through which a structure points to itself are opaque pointers, and their
    /// values are converted when they are loaded and stored.
    /// </summary>
    /// <param name="layoutType">The type of the field in its structure type.</param>
    /// <param name="fieldType">The type of the field.</param>
    /// <returns>True if the field is an opaque pointer in its structure type.</returns>
    private static bool IsOpaqueFieldPointer(TypeValue layoutType, TypeValue fieldType) =>
        layoutType is PointerType { ElementType: VoidType } &&
        fieldType is PointerType { ElementType: not VoidType };

    /// <summary>
    /// Loads the value of a field specified by the given metadata token.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeLoadField(FieldInfo field)
    {
        var fieldValue = Block.Pop();
        if (IsFieldLoadAddress(field, fieldValue))
        {
            // Load field from address
            Block.Push(fieldValue);
            MakeLoadFieldAddress(field);
            var fieldAddress = Block.Pop();
            var fieldType = ModuleBuilder.CreateType(field.FieldType);
            var layoutType = fieldAddress.GetTypeAs<PointerType>().ElementType;
            if (IsOpaqueFieldPointer(layoutType, fieldType))
            {
                var pointer = Builder.CreateLoad(Location, fieldAddress);
                Block.Push(CreateConversion(pointer, fieldType, ConvertFlags.None));
            }
            else
            {
                Block.Push(CreateLoad(
                    fieldAddress,
                    fieldType,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
        }
        else
        {
            // Load field from value
            var fieldSpan = ComputeFieldSpan(fieldValue.Type, field);

            // Check whether we have to get multiple elements
            var getField = Builder.CreateGetField(
                Location,
                fieldValue,
                fieldSpan).AsNotNull();
            if (fieldSpan.Span == 1)
            {
                var fieldType = ModuleBuilder.CreateType(field.FieldType);
                if (IsOpaqueFieldPointer(getField.Type, fieldType))
                {
                    getField = CreateConversion(
                        getField,
                        fieldType,
                        ConvertFlags.None);
                }
                Block.Push(LoadOntoEvaluationStack(
                    getField,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
            else
            {
                Block.Push(getField);
            }
        }
        RecordDelegateOrigin(
            field.FieldType,
            string.Format(
                ErrorMessages.KernelDiagnosticDelegateLoadedFromField,
                field.Name));
    }

    /// <summary>
    /// Loads the address of a field specified by the given metadata token.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeLoadFieldAddress(FieldInfo field)
    {
        var rawDeclaringType = field.DeclaringType.AsNotNull();
        // For class types (sealed/compiler-generated), CreateType returns
        // PointerType(struct) already; use the struct type as the pointer element.
        TypeValue pointerElementType;
        if (rawDeclaringType.IsClass && !rawDeclaringType.IsValueType &&
            !rawDeclaringType.IsDelegate())
        {
            pointerElementType = ModuleBuilder.CreateClassStructureType(rawDeclaringType);
        }
        else
        {
            pointerElementType = ModuleBuilder.CreateType(rawDeclaringType);
        }
        // Preserve the source pointer's address space instead of forcing
        // Generic. This prevents spurious Local→Generic AddressSpaceCasts
        // on alloca pointers, which Metal rejects as cross-space casts.
        // For non-pointer source values (value types on the stack), fall back
        // to Generic as before.
        var sourceValue = Block.Pop();
        var sourceAddrSpace = sourceValue.Type is PointerType srcPtr
            ? srcPtr.AddressSpace
            : sourceValue.Type is ViewType srcView
                ? srcView.AddressSpace
                : MemoryAddressSpace.Generic;
        var targetPointerType = ModuleBuilder.CreatePointerType(
            pointerElementType,
            sourceAddrSpace);
        var address = CreateConversion(
            sourceValue,
            targetPointerType,
            ConvertFlags.None);

        var fieldSpan = ComputeFieldSpan(targetPointerType.ElementType, field);
        var fieldAddress = Builder.CreateLoadFieldAddress(
            Location,
            address,
            fieldSpan);
        Block.Push(fieldAddress.AsNotNull());
    }

    /// <summary>
    /// Loads a static field value and returns the created IR node.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The loaded field value.</returns>
    private Value CreateLoadStaticFieldValue(FieldInfo field)
    {
        bool isCompilerGenerated = field.DeclaringType?.Name.StartsWith(
            "<>",
            StringComparison.Ordinal) == true;
        if (!isCompilerGenerated && field.FieldType.IsDelegate())
        {
            throw new KernelCompilationException(
                KernelDiagnostic.DelegateStaticFieldId,
                string.Format(
                    ErrorMessages.KernelDiagnosticDelegateStaticField,
                    $"{field.DeclaringType?.Name}.{field.Name}"),
                Location);
        }
        VerifyStaticFieldLoad(field);

        // Static field values are read when the kernel is compiled, so views and
        // pointers in them do not refer to memory of the device, and managed
        // objects in them do not exist on the device. Reject them before reading
        // the field, which would run its static constructor.
        // The static views of ILGPU itself are empty views (such as
        // ArrayView<T>.Empty), which are valid in kernels, and the fields of
        // closure classes are handled below.
        if (!isCompilerGenerated &&
            field.DeclaringType?.Assembly != typeof(ArrayView<>).Assembly)
        {
            VerifyNoMemoryReferences(field);
        }

        // Compiler-generated static fields in closure/display classes (<>c):
        // - Delegate cache fields (<>9__*): return null so brtrue takes the
        //   cache-miss path to ldftn+newobj
        // - Closure singleton (<>9): return null as the delegate handle
        //   for _delegateTable
        // Both use the delegate pointer type for consistency at phi merge
        // points in the brtrue control flow diamond.
        if (field.DeclaringType?.Name.StartsWith(
            "<>",
            StringComparison.Ordinal) == true)
        {
            var nullValue = Builder.CreateNull(Location, ModuleBuilder.DelegateType);
            if (field.FieldType.IsDelegate())
                _delegateCacheLoads.Add(nullValue);
            return nullValue;
        }

        var fieldValue = field.GetValue(null);
        return fieldValue == null ?
            Builder.CreateObjectValue(Location, field.FieldType) :
            Builder.CreateObjectValue(Location, fieldValue);
    }

    /// <summary>
    /// Rejects static fields whose values refer to memory, which does not exist
    /// on the device: views and pointers, strings and class instances.
    /// </summary>
    /// <param name="field">The static field.</param>
    /// <remarks>
    /// Static arrays and delegates are left to their own handling.
    /// </remarks>
    private void VerifyNoMemoryReferences(FieldInfo field)
    {
        // Auto-properties store their values in backing fields named after them
        const string BackingFieldSuffix = ">k__BackingField";
        string name = field.Name;
        if (name.StartsWith('<') &&
            name.EndsWith(BackingFieldSuffix, StringComparison.Ordinal))
        {
            name = name[1..^BackingFieldSuffix.Length];
        }
        string fieldName = $"{field.DeclaringType?.Name}.{name}";

        // Strings, class instances and the objects held by fields of interface
        // types; pointers count as classes in reflection
        var fieldType = field.FieldType;
        if ((fieldType.IsClass || fieldType.IsInterface) &&
            !fieldType.IsArray &&
            !fieldType.IsPointer &&
            !fieldType.IsFunctionPointer &&
            !fieldType.IsDelegate())
        {
            throw new KernelCompilationException(
                KernelDiagnostic.StaticMemoryReferenceFieldId,
                string.Format(
                    ErrorMessages.KernelDiagnosticStaticObjectField,
                    fieldName,
                    TypeDisplay.Format(fieldType)),
                Location);
        }

        if (RefersToMemory(field.FieldType))
        {
            throw new KernelCompilationException(
                KernelDiagnostic.StaticMemoryReferenceFieldId,
                string.Format(
                    ErrorMessages.KernelDiagnosticStaticMemoryReferenceField,
                    fieldName,
                    TypeDisplay.Format(field.FieldType)),
                Location);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if values of the given type refer to
    /// memory: pointers, all types that map to views or pointers in the IR, and
    /// structures and arrays containing them.
    /// </summary>
    /// <param name="type">The managed type.</param>
    /// <param name="visited">
    /// The structures already being checked, since a structure can contain an
    /// array of itself.
    /// </param>
    /// <remarks>
    /// Views are detected by the IR type the compiler maps a type to, so that
    /// every view type is covered without listing them here. Fields of class
    /// types, which the IR also represents as pointers, are not followed, since
    /// kernels reject loading class instances from static fields anyway.
    /// </remarks>
    private bool RefersToMemory(Type type, HashSet<Type>? visited = null)
    {
        // Arrays and immutable arrays contain the values of their elements
        while (true)
        {
            if (type.IsArray)
                type = type.GetElementType().AsNotNull();
            else if (type.IsImmutableArray(out var elementType))
                type = elementType;
            else
                break;
        }
        if (type.IsPointer || type.IsByRef)
            return true;
        // Primitive types never refer to memory, although the IR maps native
        // integers to pointer-sized types that depend on the address space
        if (!type.IsValueType || type.IsPrimitive)
            return false;

        var typeValue = ModuleBuilder.CreateType(type);
        if (!typeValue.HasFlags(TypeFlags.AddressSpaceDependent))
            return false;
        if (typeValue is AddressSpaceType)
            return true;

        // Check whether the address dependency stems from a view or a pointer
        // rather than from a field of a class type
        visited ??= [];
        if (!visited.Add(type))
            return false;
        foreach (var field in type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (RefersToMemory(field.FieldType, visited))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Loads a static field value.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeLoadStaticField(FieldInfo field) =>
        Block.Push(CreateLoadStaticFieldValue(field));

    /// <summary>
    /// Loads the address of a static field specified by the given metadata token.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeLoadStaticFieldAddress(FieldInfo field)
    {
        var fieldValue = CreateLoadStaticFieldValue(field);
        var tempAlloca = CreateTempAlloca(fieldValue.Type);
        Builder.CreateStore(Location, tempAlloca, fieldValue);
        Block.Push(tempAlloca);
    }

    /// <summary>
    /// Stores a value to a field.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeStoreField(FieldInfo field)
    {
        // Values captured by closures are verified once all methods have been
        // generated (see ClosureCaptures)
        var declaringType = field.DeclaringType;
        if (ClosureCaptures is not null &&
            declaringType is not null &&
            !field.FieldType.IsClass &&
            declaringType.IsDefined(typeof(CompilerGeneratedAttribute), false))
        {
            ClosureCaptures.Add(
                Block.GetValue(new VariableRef(
                    Block.StackCounter - 1,
                    VariableRefType.Stack)),
                Method.GetDisplayName(),
                field.Name,
                Location);
        }

        var fieldType = ModuleBuilder.CreateType(field.FieldType);
        var value = Block.Pop(
            fieldType,
            field.FieldType.ToTargetUnsignedFlags());
        MakeLoadFieldAddress(field);
        var address = Block.Pop();
        var layoutType = address.GetTypeAs<PointerType>().ElementType;
        if (IsOpaqueFieldPointer(layoutType, fieldType))
            value = CreateConversion(value, layoutType, ConvertFlags.None);
        CreateStore(address, value);
    }

    /// <summary>
    /// Stores a value to a static field.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeStoreStaticField(FieldInfo field)
    {
        VerifyStaticFieldStore(field);

        // Consume the current value from the stack but do not emit a global store,
        // since we don't have any valid target address.
        // TODO: Stores to static fields could be automatically propagated to the
        // .Net runtime after kernel invocation. However, this remains as a future
        // feature.
        Block.Pop();
    }
}
