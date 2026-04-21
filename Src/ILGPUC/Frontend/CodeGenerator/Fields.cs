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

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Reflection;

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
    /// Loads the value of a field specified by the given metadata token.
    /// </summary>
    /// <param name="field">The field.</param>
    private void MakeLoadField(FieldInfo field)
    {
        var fieldValue = Block.Pop();
        if (fieldValue.Type is PointerType)
        {
            // Load field from address
            Block.Push(fieldValue);
            MakeLoadFieldAddress(field);
            var fieldAddress = Block.Pop();
            var fieldType = ModuleBuilder.CreateType(field.FieldType);
            Block.Push(CreateLoad(
                fieldAddress,
                fieldType,
                field.FieldType.ToTargetUnsignedFlags()));
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
                Block.Push(LoadOntoEvaluationStack(
                    getField,
                    field.FieldType.ToTargetUnsignedFlags()));
            }
            else
            {
                Block.Push(getField);
            }
        }
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
        VerifyStaticFieldLoad(field);

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
            var delegatePointerType = ModuleBuilder.CreatePointerType(
                ModuleBuilder.IntPointerType,
                MemoryAddressSpace.Generic);
            return Builder.CreateNull(Location, delegatePointerType);
        }

        var fieldValue = field.GetValue(null);
        return fieldValue == null ?
            Builder.CreateObjectValue(Location, field.FieldType) :
            Builder.CreateObjectValue(Location, fieldValue);
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
        var fieldType = ModuleBuilder.CreateType(field.FieldType);
        var value = Block.Pop(
            fieldType,
            field.FieldType.ToTargetUnsignedFlags());
        MakeLoadFieldAddress(field);
        var address = Block.Pop();
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
