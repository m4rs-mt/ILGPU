// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeInformationManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPUC.IR;

/// <summary>
/// Represents a context that manages type information.
/// </summary>
sealed class TypeInformationManager
{
    #region Nested Types

    /// <summary>
    /// Represents a type information about a managed type.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    internal sealed class TypeInformation
    {
        #region Instance

        /// <summary>
        /// Constructs a new type information.
        /// </summary>
        /// <param name="parent">The parent type manager.</param>
        /// <param name="type">The .Net type.</param>
        /// <param name="size">The size in bytes (if any).</param>
        /// <param name="fields">All managed fields.</param>
        /// <param name="fieldOffsets">All field offsets.</param>
        /// <param name="fieldTypes">All managed field types.</param>
        /// <param name="numFlattenedFields">The number of flattened fields.</param>
        internal TypeInformation(
            TypeInformationManager parent,
            Type type,
            int size,
            ImmutableArray<FieldInfo> fields,
            ImmutableArray<int> fieldOffsets,
            ImmutableArray<Type> fieldTypes,
            int numFlattenedFields)
        {
            Parent = parent;
            ManagedType = type;
            Size = size;
            Fields = fields;
            FieldOffsets = fieldOffsets;
            FieldTypes = fieldTypes;
            NumFlattenedFields = numFlattenedFields;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent information manager.
        /// </summary>
        public TypeInformationManager Parent { get; }

        /// <summary>
        /// Returns the .Net type.
        /// </summary>
        public Type ManagedType { get; }

        /// <summary>
        /// Returns the type size in bytes (if any).
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Returns the number of fields.
        /// </summary>
        public int NumFields => Fields.Length;

        /// <summary>
        /// Returns all fields.
        /// </summary>
        public ImmutableArray<FieldInfo> Fields { get; }

        /// <summary>
        /// Returns all field types.
        /// </summary>
        public ImmutableArray<Type> FieldTypes { get; }

        /// <summary>
        /// Returns all field offsets.
        /// </summary>
        public ImmutableArray<int> FieldOffsets { get; }

        /// <summary>
        /// Returns the number of flattened fields a value of this type occupies.
        /// Values of reference types are pointers and occupy a single field.
        /// </summary>
        public int NumFlattenedFields { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets nested field type information.
        /// </summary>
        /// <param name="index">The field index.</param>
        /// <returns>The resulting type information.</returns>
        public TypeInformation GetFieldTypeInfo(int index) =>
            Parent.GetTypeInfo(FieldTypes[index]);

        /// <summary>
        /// Gets the absolute index of the given field.
        /// </summary>
        /// <param name="info">The field to get.</param>
        /// <returns>The absolute field index.</returns>
        public int GetAbsoluteIndex(FieldInfo info)
        {
            for (int i = 0, e = NumFields; i < e; ++i)
            {
                if (Fields[i] == info)
                    return FieldOffsets[i];
            }
            return -1;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this type.
        /// </summary>
        /// <returns>The string representation of this type.</returns>
        public override string ToString() => ManagedType.Name;

        #endregion
    }

    #endregion

    #region Instance

    private readonly Dictionary<Type, TypeInformation> _typeInfoMapping = [];

    /// <summary>
    /// Constructs a new type context.
    /// </summary>
    public TypeInformationManager()
    {
        AddTypeInfo(typeof(bool));

        AddTypeInfo(typeof(byte));
        AddTypeInfo(typeof(ushort));
        AddTypeInfo(typeof(uint));
        AddTypeInfo(typeof(ulong));
        AddWideIntegerTypeInfo(typeof(UInt128));
        AddTypeInfo(typeof(UIntPtr));

        AddTypeInfo(typeof(sbyte));
        AddTypeInfo(typeof(short));
        AddTypeInfo(typeof(int));
        AddTypeInfo(typeof(long));
        AddWideIntegerTypeInfo(typeof(Int128));
        AddTypeInfo(typeof(IntPtr));

        AddTypeInfo(typeof(float));
        AddTypeInfo(typeof(double));

        AddTypeInfo(typeof(char));
        AddTypeInfo(typeof(string));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resolves type information for the given type.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns>The resolved type information.</returns>
    public TypeInformation GetTypeInfo(Type type)
    {
        lock (_typeInfoMapping)
        {
            if (!_typeInfoMapping.TryGetValue(type, out TypeInformation? typeInfo))
            {
                // Synchronize all accesses below using a write scope
                typeInfo = CreateTypeInfo(type);
            }
            return typeInfo;
        }
    }

    /// <summary>
    /// Resolves type information for the given type.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns>The resolved type information.</returns>
    private TypeInformation GetTypeInfoInternal(Type type)
    {
        if (!_typeInfoMapping.TryGetValue(type, out TypeInformation? typeInfo))
            typeInfo = CreateTypeInfo(type);
        return typeInfo;
    }

    /// <summary>
    /// Adds type information for a type that occupies a single field without
    /// any nested fields.
    /// </summary>
    /// <param name="type">The type to add.</param>
    /// <returns>The created type information instance.</returns>
    private TypeInformation AddTypeInfo(Type type)
    {
        var result = new TypeInformation(
            this,
            type,
            0,
            ImmutableArray<FieldInfo>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<Type>.Empty,
            1);
        _typeInfoMapping.Add(type, result);
        return result;
    }

    /// <summary>
    /// Adds the type information of <see cref="Int128"/> or
    /// <see cref="UInt128"/>, which are passed as a whole like primitive types,
    /// but are represented by two 64-bit fields in kernels. The fields count
    /// as two flattened fields, so that the fields that follow them in a
    /// structure map to the right fields of its kernel representation.
    /// </summary>
    /// <param name="type">The type to add.</param>
    private void AddWideIntegerTypeInfo(Type type) =>
        _typeInfoMapping.Add(type, new TypeInformation(
            this,
            type,
            0,
            ImmutableArray<FieldInfo>.Empty,
            ImmutableArray<int>.Empty,
            ImmutableArray<Type>.Empty,
            2));

    /// <summary>
    /// Creates new type information and registers the created object
    /// in the internal cache.
    /// </summary>
    /// <param name="type">The base .Net type.</param>
    /// <returns>The created type information object.</returns>
    /// <remarks>
    /// Pointers, arrays and views refer to their elements and occupy a single
    /// field without resolving their element types, which may refer back to
    /// the types containing them.
    /// </remarks>
    private TypeInformation CreateTypeInfo(Type type)
    {
        if (type.IsPointer || type.IsByRef || type.IsArray || type.IsEnum ||
            type.IsArrayViewType(out _))
        {
            return AddTypeInfo(type);
        }

        var result = CreateCompoundTypeInfo(type);
        _typeInfoMapping.Add(type, result);
        return result;
    }

    /// <summary>
    /// Creates new type information for compound types.
    /// </summary>
    /// <param name="type">The base .Net type.</param>
    /// <returns>The created type information object.</returns>
    private TypeInformation CreateCompoundTypeInfo(Type type)
    {
        var fieldArray = GetLayoutFields(type);

        int size = 0;
        if (type.IsValueType && type.StructLayoutAttribute != null)
        {
            // NB: In C# 8, System.ValueTuple is considered 'unmanaged' if all of its
            // fields are unmanaged types.
            if (type.IsValueTuple())
            {
                // System.ValueTuple uses LayoutKind.Auto, so the fields may not be
                // strictly in sequential order. Therefore, we reorder the fields
                // according to the field offsets.
                var genericArgs = type.GetGenericArguments();
                var offsets = ValueTuples.GetOffsets(genericArgs);
                Array.Sort(offsets, fieldArray);

                // Retrieve the size of the ValueTuple, which may include trailing
                // padding bytes.
                size = type.SizeOf();
            }
            else if (type.StructLayoutAttribute.Value == LayoutKind.Sequential)
            {
                size = type.StructLayoutAttribute.Size;
            }
            // Closure structs the C# compiler generates for local functions
            // use LayoutKind.Auto. They only live inside kernels and never
            // cross the host boundary, so their fields are laid out in
            // declaration order like sequential structures.
            else if (type.StructLayoutAttribute.Value != LayoutKind.Auto
                || !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                throw new NotSupportedException(
                    string.Format(
                        ErrorMessages.NotSupportedStructureLayout,
                        type));
            }
        }

        var fields = ImmutableArray.Create(fieldArray);
        var fieldTypesBuilder = ImmutableArray.CreateBuilder<Type>(fields.Length);
        var fieldOffsetsBuilder = ImmutableArray.CreateBuilder<int>(fields.Length);
        int flattenedFields = 0;
        foreach (var field in fields)
        {
            fieldOffsetsBuilder.Add(flattenedFields);
            fieldTypesBuilder.Add(field.FieldType);

            // Fields of reference types are pointers occupying a single field.
            // Their types are not resolved here, as they may refer back to the
            // types containing them. Nested structures cannot contain themselves.
            if (!field.FieldType.IsValueType)
            {
                ++flattenedFields;
                continue;
            }

            // Empty structures are treated as having a single field.
            var nestedTypeInfo = GetTypeInfoInternal(field.FieldType);
            flattenedFields += nestedTypeInfo.NumFlattenedFields > 0
                ? nestedTypeInfo.NumFlattenedFields
                : 1;
        }

        // Kernels lay out all fields at their natural alignments and round all
        // sizes up to the alignment, which the runtime only does as well if the
        // packing and the explicit sizes of this structure and of its nested
        // structures keep that layout
        if (type.IsValueType && !type.IsValueTuple() && !HasNaturalLayout(type))
        {
            throw new NotSupportedException(
                string.Format(
                    ErrorMessages.NotSupportedPackedStructureLayout,
                    type));
        }

        return new TypeInformation(
            this,
            type,
            size,
            fields,
            fieldOffsetsBuilder.MoveToImmutable(),
            fieldTypesBuilder.MoveToImmutable(),
            type.IsValueType ? flattenedFields : 1);
    }

    /// <summary>
    /// Returns the instance fields of the given type in layout order.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The instance fields in layout order.</returns>
    private static FieldInfo[] GetLayoutFields(Type type)
    {
        var fieldArray = type.GetFields(
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(fieldArray, (left, right) =>
            left.MetadataToken.CompareTo(right.MetadataToken));

        // Handle [InlineArray(N)]: the C# compiler declares such structs
        // with a single element field but the struct is sized as N copies
        // of that field. Replicate the field N times so the IR sees the
        // full layout. Without this, struct sizes and field access
        // through Unsafe.Add are wrong (e.g. IntBuffer4 { int _element0; }
        // decorated with [InlineArray(4)] would appear to be 1 int / 4
        // bytes instead of 4 ints / 16 bytes).
        var inlineArrayAttr = type.GetCustomAttribute<
            System.Runtime.CompilerServices.InlineArrayAttribute>(inherit: false);
        if (inlineArrayAttr is not null
            && fieldArray.Length == 1
            && inlineArrayAttr.Length > 1)
        {
            var original = fieldArray[0];
            var expanded = new FieldInfo[inlineArrayAttr.Length];
            for (int i = 0; i < inlineArrayAttr.Length; i++)
                expanded[i] = original;
            fieldArray = expanded;
        }
        return fieldArray;
    }

    /// <summary>
    /// Returns true if the runtime lays out the given structure type like
    /// kernels do: with all fields at the offsets of their natural alignments
    /// and with a size that is a multiple of its natural alignment.
    /// </summary>
    /// <param name="type">The structure type.</param>
    /// <returns>True if the runtime lays out the type like kernels do.</returns>
    private static bool HasNaturalLayout(Type type)
    {
        var naturalOffsets = new List<int>();
        var runtimeOffsets = new List<int>();
        var natural = ComputeLayout(type, false, naturalOffsets);
        var runtime = ComputeLayout(type, true, runtimeOffsets);
        return natural.Size == runtime.Size &&
            naturalOffsets.SequenceEqual(runtimeOffsets);
    }

    /// <summary>
    /// Computes the layout of the given type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="packed">
    /// True to compute the layout of the runtime, which applies the packing and
    /// the explicit sizes of sequential structures, false to compute the layout
    /// of kernels, which align all fields naturally and round explicit sizes up
    /// to the natural alignment of their structures.
    /// </param>
    /// <param name="offsets">Receives the offsets of all fields (if any).</param>
    /// <returns>The size and the alignment of the type.</returns>
    private static (int Size, int Alignment) ComputeLayout(
        Type type,
        bool packed,
        List<int>? offsets = null)
    {
        if (!type.IsValueType)
            return (IntPtr.Size, IntPtr.Size);
        if (type.IsPrimitive || type.IsEnum)
        {
            int primitiveSize = type.SizeOf();
            return (primitiveSize, primitiveSize);
        }

        var layout = type.StructLayoutAttribute;
        bool isSequential = layout is { Value: LayoutKind.Sequential } &&
            !type.IsValueTuple();
        int pack = packed && isSequential && layout!.Pack > 0
            ? layout.Pack
            : int.MaxValue;
        int offset = 0;
        int alignment = 1;
        foreach (var field in GetLayoutFields(type))
        {
            var (fieldSize, fieldAlignment) = ComputeLayout(field.FieldType, packed);
            fieldAlignment = Math.Min(fieldAlignment, pack);
            offset = TypeValue.Align(offset, fieldAlignment);
            offsets?.Add(offset);
            offset += fieldSize;
            alignment = Math.Max(alignment, fieldAlignment);
        }

        int explicitSize = isSequential ? layout!.Size : 0;
        int size = packed
            ? Math.Max(TypeValue.Align(offset, alignment), explicitSize)
            : TypeValue.Align(Math.Max(offset, explicitSize), alignment);
        return (Math.Max(size, 1), alignment);
    }

    #endregion
}
