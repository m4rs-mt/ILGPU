// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a structure type.
/// </summary>
sealed partial class StructureType : ObjectType
{
    #region Nested Types

    /// <summary>
    /// A structure type builder.
    /// </summary>
    internal sealed class Builder
    {
        #region Instance

        private TypeFlags _typeFlags;
        private InlineList<TypeValue> _fieldsBuilder;
        private InlineList<TypeValue> _allFieldsBuilder;
        private InlineList<int> _offsetsBuilder;

        /// <summary>
        /// Creates a new type builder with the given capacity.
        /// </summary>
        /// <param name="moduleBuilder">The current module builder.</param>
        /// <param name="capacity">The initial capacity.</param>
        /// <param name="size">The custom size in bytes (if any).</param>
        internal Builder(ModuleBuilder moduleBuilder, int capacity, int size)
        {
            Debug.Assert(capacity >= 0, "Invalid capacity");
            Debug.Assert(size >= 0, "Invalid size");

            _typeFlags = TypeFlags.None;

            _fieldsBuilder = InlineList<TypeValue>.Create(capacity);
            _allFieldsBuilder = InlineList<TypeValue>.Create(capacity);
            _offsetsBuilder = InlineList<int>.Create(capacity);
            ModuleBuilder = moduleBuilder;

            ExplicitSize = size;
            Alignment = Offset = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent module builder.
        /// </summary>
        public ModuleBuilder ModuleBuilder { get; }

        /// <summary>
        /// Returns the explicit size in bytes (if any).
        /// </summary>
        public int ExplicitSize { get; }

        /// <summary>
        /// Returns the number of all fields.
        /// </summary>
        public int Count => _allFieldsBuilder.Count;

        /// <summary>
        /// The current offset in bytes.
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// The current alignment for the underlying type.
        /// </summary>
        public int Alignment { get; internal set; }

        /// <summary>
        /// The current size in bytes.
        /// </summary>
        public int Size => Math.Max(AlignedSize, ExplicitSize);

        /// <summary>
        /// Returns the aligned size based on the current offset and the alignment.
        /// </summary>
        public int AlignedSize => Alignment < 1 ? 0 : Align(Offset, Alignment);

        /// <summary>
        /// Returns the field type that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public TypeValue this[FieldAccess access] =>
            _allFieldsBuilder[access.Index];

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given type node to the structure builder.
        /// </summary>
        /// <param name="type">The type node to add.</param>
        public void Add(TypeValue? type)
        {
            if (type is null) return;

            _fieldsBuilder.Add(type);
            if (type is StructureType structureType)
            {
                // Add initial field using structure alignment information
                int baseOffset = Offset;
                AddInternal(
                    structureType[0],
                    0,
                    structureType.Alignment);

                // Add remaining fields
                int currentOffset = 0;
                for (int i = 1, e = structureType.NumFields; i < e; ++i)
                {
                    var fieldType = structureType[i];
                    int nextOffset = structureType.GetOffset(i);
                    AddInternal(
                        fieldType,
                        nextOffset - currentOffset,
                        fieldType.Alignment);
                    currentOffset = nextOffset;
                }
                int lastFieldSize = structureType[
                    structureType.NumFields - 1].Size;
                Offset = Align(Offset + lastFieldSize, type.Alignment);

                // Take a custom structure size into account
                int structSize = Offset - baseOffset;
                Offset += Math.Max(structureType.Size - structSize, 0);
            }
            else
            {
                AddInternal(type, 0, type.Alignment);
                Offset += type.Size;
            }

            // Adjust offset and alignment information
            Alignment = Math.Max(Alignment, type.Alignment);
        }

        /// <summary>
        /// Adds the given primitive type node.
        /// </summary>
        /// <param name="type">The type node to add.</param>
        /// <param name="offset">The custom relative offset.</param>
        /// <param name="alignment">The custom alignment.</param>
        private void AddInternal(TypeValue type, int offset, int alignment)
        {
            type.Assert(type is not StructureType && offset >= 0);

            _allFieldsBuilder.Add(type);
            _typeFlags |= type.Flags;

            // Align the next field properly
            Offset = Align(Offset + offset, alignment);
            _offsetsBuilder.Add(Offset);
        }

        /// <summary>
        /// Seals this builder and returns a type that corresponds to the type
        /// represented by this builder.
        /// </summary>
        /// <returns>The create type node.</returns>
        public TypeValue Seal()
        {
            // Check for a special case in which we require custom padding elements
            // NB: Prevent empty structures by ensuring we have at least one field
            // that is not padding.
            if (Count == 0 && AlignedSize < Size)
                Add(ModuleBuilder.Padding8Type.PrimitiveType);

            // If the structure has a single field, apply padding using the size
            // of the initial field. This allows us to fill a fixed buffer with
            // the correct/aligned type.
            if (Count == 1 && AlignedSize < Size)
            {
                var basicValueType = _allFieldsBuilder[0].BasicValueType;
                var paddingType = ModuleBuilder.GetPaddingType(basicValueType);
                for (int i = AlignedSize, e = Size; i < e; i += paddingType.Size)
                    Add(paddingType);
            }

            // Ensure that the structure is populated to its required size before
            // we finalize. CAUTION: we have to ignore cases in which this type
            // contains types that will be lowered in future stages.
            if ((_typeFlags & TypeFlags.ViewDependent) == TypeFlags.None)
            {
                for (int i = AlignedSize, e = Size; i < e; ++i)
                    Add(ModuleBuilder.Padding8Type);
            }
            return ModuleBuilder.FinishStructureType(this);
        }

        /// <summary>
        /// Moves the underlying builders to immutable arrays.
        /// </summary>
        /// <param name="types">Direct field types.</param>
        /// <param name="allTypes">All field types.</param>
        /// <param name="offsets">All field offsets.</param>
        /// <param name="remappedFields">Renumbered field indices.</param>
        internal void Seal(
            out ReadOnlyMemory<TypeValue> types,
            out ReadOnlyMemory<TypeValue> allTypes,
            out ReadOnlyMemory<int> offsets,
            out ReadOnlyMemory<int> remappedFields)
        {
            // Calculate a mapping from the original field indices to new
            // field indices, taking into account any padding fields.
            var remapBuilder = InlineList<int>.Create(_allFieldsBuilder.Capacity);
            for (var i = 0; i < _allFieldsBuilder.Count; i++)
            {
                if (_allFieldsBuilder[i] is not PaddingType)
                    remapBuilder.Add(i);
            }

            // Finalize the structure type.
            types = _fieldsBuilder.AsReadOnlyMemory();
            allTypes = _allFieldsBuilder.AsReadOnlyMemory();
            offsets = _offsetsBuilder.AsReadOnlyMemory();
            remappedFields = remapBuilder.AsReadOnlyMemory();

            Debug.Assert(
                types.Length <= allTypes.Length &&
                offsets.Length == allTypes.Length &&
                remappedFields.Length <= allTypes.Length,
                "Broken builder");
        }

        #endregion
    }

    /// <summary>
    /// An enumerator to enumerate all nested fields in the structure type.
    /// </summary>
    /// <param name="type">The structure type.</param>
    internal new struct Enumerator(StructureType type)
    {
        private int _index = -1;

        /// <summary>
        /// Returns the current use.
        /// </summary>
        public readonly (TypeValue, FieldAccess) Current =>
            (type.Fields[_index], new FieldAccess(_index));

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => ++_index < type.NumFields;
    }

    /// <summary>
    /// A readonly collection of all field offsets and paddings.
    /// </summary>
    /// <param name="parent">The parent structure type.</param>
    internal readonly struct OffsetCollection(StructureType parent)
    {
        /// <summary>
        /// An enumerator to enumerate all offsets in the structure type.
        /// </summary>
        /// <remarks>
        /// The tuple contains field access, byte offset and byte padding info.
        /// </remarks>
        /// <param name="type">The structure type.</param>
        internal struct Enumerator(StructureType type)
        {
            private StructureType.Enumerator _enumerator = type.GetEnumerator();
            private int _currentPadding;
            private int _offset;

            /// <summary>
            /// Returns the current use.
            /// </summary>
            public readonly (FieldAccess, int, int) Current
            {
                get
                {
                    var (_, access) = _enumerator.Current;
                    return (access, type.GetOffset(access), _currentPadding);
                }
            }

            /// <inheritdoc cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                // Adjust current offset
                if (!_enumerator.MoveNext())
                    return false;

                // Align the current offset
                var (nextType, access) = _enumerator.Current;

                // Compute the current offsets
                int alignedOffset = Align(_offset, nextType.Alignment);
                int fieldOffset = type.GetOffset(access);
                _currentPadding = fieldOffset - alignedOffset;
                Debug.Assert(_currentPadding >= 0, "Invalid padding");

                // Adjust the next offset
                _offset = fieldOffset + nextType.Size;
                return true;
            }
        }

        /// <summary>
        /// Returns the number of offsets.
        /// </summary>
        public int Count => parent.NumFields;

        /// <summary>
        /// Returns an enumerator to enumerate all offsets in the parent type.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(parent);
    }

    /// <summary>
    /// Contains all vectorizable field ranges in the scope of its parent type.
    /// </summary>
    internal readonly struct VectorizableFieldCollection
    {
        #region Nested Types

        /// <summary>
        /// Represents a vectorizable sub range in the scope of a structure type.
        /// </summary>
        internal struct Entry(TypeValue type, int index, int offset, int count = 1)
        {
            #region Properties

            /// <summary>
            /// Returns the associated type.
            /// </summary>
            public TypeValue Type { get; } = type;

            /// <summary>
            /// Returns the start index within the parent structure.
            /// </summary>
            public int Index { get; } = index;

            /// <summary>
            /// Returns the number of fields.
            /// </summary>
            public int Count { readonly get; private set; } = count;

            /// <summary>
            /// Returns the base offset in bytes from the beginning of the field.
            /// </summary>
            public int Offset { get; } = offset;

            /// <summary>
            /// Returns the size of this chunk in bytes.
            /// </summary>
            public readonly int Size => Count * Type.Size;

            /// <summary>
            /// Returns the required alignment in bytes.
            /// </summary>
            public readonly int RequiredAlignment =>
                // The alignment is equal to the size in bytes
                Size;

            #endregion

            #region Methods

            /// <summary>
            /// Splits the current entry into two parts.
            /// </summary>
            /// <param name="first">The first part.</param>
            /// <param name="second">The second part.</param>
            public readonly void Split(out Entry first, out Entry second)
            {
                Type.Assert(Count > 1);

                int firstCount = Count >> 1 + Count % 2;
                first = new Entry(Type, Index, Offset, firstCount);
                second = new Entry(
                    Type,
                    Index + firstCount,
                    Offset + Type.Size * firstCount,
                    Count - firstCount);
            }

            /// <summary>
            /// Adds a field to this entry.
            /// </summary>
            public void AddField() => ++Count;

            /// <summary>
            /// Checks whether the base offset is properly alignment with respect to
            /// the given alignment in bytes.
            /// </summary>
            /// <param name="alignment">The underlying alignment in bytes.</param>
            /// <returns>True, if the range is properly aligned.</returns>
            public readonly bool IsAligned(int alignment) =>
                // Check for a proper alignment of the base address
                alignment % RequiredAlignment == 0;

            /// <summary>
            /// Returns true if this entry can be properly aligned.
            /// </summary>
            /// <param name="parentType">The parent structure type.</param>
            public readonly bool CanBeAligned(StructureType parentType)
            {
                int requiredAlignment = RequiredAlignment;
                return
                    // Check for a relative alignment inside the structure
                    Offset % requiredAlignment == 0 &&
                    // Check for a relative alignment of odd structure accesses
                    (Offset + parentType.Size) % requiredAlignment == 0;
            }

            #endregion
        }

        #endregion

        #region Instance

        private readonly List<Entry> _ranges;

        /// <summary>
        /// Constructs a new field collection.
        /// </summary>
        /// <param name="structureType">The parent structure type.</param>
        /// <param name="maxSizeInBytes">The maximum vector size in bytes.</param>
        internal VectorizableFieldCollection(
            StructureType structureType,
            int maxSizeInBytes)
        {
            structureType.Assert(structureType.NumFields > 0);
            _ranges = new List<Entry>(structureType.NumFields);

            var current = new Entry(
                structureType[0],
                0,
                structureType.GetOffset(0));
            int currentOffset = current.Offset;

            for (int i = 1, e = structureType.NumFields; i < e; ++i)
            {
                var nextType = structureType[i];
                var nextOffset = structureType.GetOffset(i);
                // If the next type is not compatible or is not properly aligned
                // we have to split at this point
                if (!current.Type.Equals(nextType) ||
                    currentOffset + nextType.Size != nextOffset ||
                    current.Size + nextType.Size > maxSizeInBytes)
                {
                    // Register the current vectorizable entry
                    RegisterRange(structureType, current);
                    current = new Entry(
                        nextType,
                        i,
                        nextOffset);
                }
                else
                {
                    // Everything seems to be compatible -> continue processing
                    current.AddField();
                }
                currentOffset = nextOffset;
            }

            // Add the last entry
            RegisterRange(structureType, current);
        }

        /// <summary>
        /// Registers the given range entry.
        /// </summary>
        /// <param name="structureType">The parent structure type.</param>
        /// <param name="entry">The entry to register.</param>
        private void RegisterRange(StructureType structureType, in Entry entry)
        {
            int offset = entry.Offset;
            for (
                int index = 0, stepSize = entry.Count < 4 ? 2 : 4;
                index < entry.Count;
                stepSize >>= 1)
            {
                for (; index + stepSize <= entry.Count; index += stepSize)
                {
                    var newEntry = new Entry(
                        entry.Type,
                        index + entry.Index,
                        offset,
                        stepSize);
                    if (newEntry.Count > 1 && !newEntry.CanBeAligned(structureType))
                    {
                        newEntry.Split(out var first, out var second);
                        RegisterRange(structureType, first);
                        RegisterRange(structureType, second);
                    }
                    else
                    {
                        // The entry is properly aligned
                        _ranges.Add(newEntry);
                    }
                    offset += entry.Type.Size * stepSize;
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of entries.
        /// </summary>
        public readonly int Count => _ranges.Count;

        /// <summary>
        /// Returns the i-th entry.
        /// </summary>
        /// <param name="index">The entry index.</param>
        /// <returns>The i-th vector range entry.</returns>
        public readonly Entry this[int index] => _ranges[index];

        #endregion
    }

    #endregion

    #region Static

    /// <summary>
    /// Gets the number of fields of the given type.
    /// </summary>
    /// <param name="typeNode">The type.</param>
    /// <returns>The number of nested fields (or 1).</returns>
    public static int GetNumFields(TypeValue typeNode) =>
        typeNode is StructureType structureType
        ? structureType.NumFields
        : 1;

    /// <summary>
    /// Gets the field name of a managed structure type.
    /// </summary>
    /// <param name="fieldIndex">The field index.</param>
    /// <returns>The managed field name within a structure type.</returns>
    public static string GetFieldName(int fieldIndex) =>
        "Field" + fieldIndex;

    #endregion

    #region Instance

    /// <summary>
    /// Caches the internal hash code of all child nodes.
    /// </summary>
    private readonly int _hashCode;

    /// <summary>
    /// All underlying byte offsets.
    /// </summary>
    private readonly ReadOnlyMemory<int> _offsets;

    /// <summary>
    /// Maps the original field index to the index of the rebuilt field.
    /// </summary>
    private readonly ReadOnlyMemory<int> _remappedFields;

    /// <summary>
    /// Stores all direct fields.
    /// </summary>
    private readonly ReadOnlyMemory<TypeValue> _fields;

    /// <summary>
    /// Stores all fields.
    /// </summary>
    private readonly ReadOnlyMemory<TypeValue> _allFields;

    /// <summary>
    /// Constructs a new object type.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="builder">The current structure builder.</param>
    public StructureType(in ModuleValueInitializer initializer, Builder builder)
        : base(initializer, initializer.Builder.KindType)
    {
        Alignment = builder.Alignment;
        Size = builder.Size;

        builder.Seal(
            out var fields,
            out var allFields,
            out _offsets,
            out _remappedFields);
        _fields = fields;
        _allFields = allFields;
        Seal(initializer.Builder.KindType);

        // Update flags and init hash code
        _hashCode = base.GetHashCode();
        for (int i = 0, e = NumFields; i < e; ++i)
        {
            var type = Fields[i];
            _hashCode = HashCode.Combine(_hashCode, type, _offsets.Span[i]);
            AddFlags(type.Flags);
        }
        AddFlags(TypeFlags.StructureDependent);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the high-level fields stored in this structure type.
    /// </summary>
    public ReadOnlySpan<TypeValue> DirectFields => _fields.Span;

    /// <summary>
    /// Returns all associated fields.
    /// </summary>
    public ReadOnlySpan<TypeValue> Fields => _allFields.Span;

    /// <summary>
    /// Returns a readonly collection of all field offsets.
    /// </summary>
    public OffsetCollection Offsets => new(this);

    /// <summary>
    /// Returns the number of associated fields.
    /// </summary>
    public int NumFields => Fields.Length;

    /// <summary>
    /// Returns the field type that corresponds to the given field access.
    /// </summary>
    /// <param name="fieldAccess">The field access.</param>
    /// <returns>The resolved field type.</returns>
    public TypeValue this[FieldAccess fieldAccess] => Fields[fieldAccess.Index];

    #endregion

    #region Methods

    /// <summary>
    /// Returns a readonly collection of all vectorized field configurations that
    /// do not exceed the maximum size in bytes.
    /// </summary>
    /// <param name="maxSizeInBytes">The maximum vector size in bytes.</param>
    /// <returns>The vectorizable field collection.</returns>
    public VectorizableFieldCollection GetVectorizableFields(int maxSizeInBytes) =>
        new(this, maxSizeInBytes);

    /// <summary>
    /// Gets a specific field offset in bytes from the beginning of the structure.
    /// </summary>
    /// <param name="fieldAccess">The field reference.</param>
    /// <returns>The field offset in bytes.</returns>
    public int GetOffset(FieldAccess fieldAccess) =>
        _offsets.Span[fieldAccess.Index];

    /// <summary>
    /// Gets the remapped field index corresponding to the original structure.
    /// </summary>
    /// <param name="fieldIndex">The field index.</param>
    /// <returns>The adjusted field index.</returns>
    public int RemapFieldIndex(int fieldIndex) =>
        _remappedFields.Span[fieldIndex];

    /// <summary>
    /// Returns true if any field of this structure has a pointer type.
    /// Used by backends (Metal, OpenCL) to determine whether view structs need to
    /// be flattened into separate kernel parameters.
    /// </summary>
    public bool HasAnyPointerField()
    {
        foreach (var field in Fields)
        {
            if (field is PointerType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a nested type that corresponds to the given span.
    /// </summary>
    /// <param name="moduleBuilder">The module builder.</param>
    /// <param name="span">The span to slice.</param>
    /// <returns>The nested type.</returns>
    public TypeValue Get(ModuleBuilder moduleBuilder, FieldSpan span) =>
        !span.HasSpan
        ? this[span.Access]
        : span.Index == 0 && span.Span == NumFields
            ? this
            : Slice(moduleBuilder, span);

    /// <summary>
    /// Converts all field types using the type converter provided.
    /// </summary>
    /// <param name="moduleBuilder">The module builder.</param>
    /// <param name="typeConverter">The type converter instance to use.</param>
    /// <returns></returns>
    public StructureType ConvertFieldTypes(
        ModuleBuilder moduleBuilder,
        Func<TypeValue, TypeValue>? typeConverter = null)
    {
        // Iterate over all direct fields to preserve the high-level layout
        var builder = moduleBuilder.CreateStructureType(NumFields);
        bool changed = false;
        foreach (var type in DirectFields)
        {
            // Convert type and append it
            var convertedType = typeConverter?.Invoke(type) ?? type;
            builder.Add(convertedType);
            changed |= convertedType != type;
        }
        builder.Alignment = Alignment;

        // Ensure that we did not lose any fields
        this.Assert(
            builder.Count >= NumFields,
            $"Field count after remapping ({builder.Count}) is less than NumFields" +
            $"({NumFields})");

        // Create final structure type
        var result = builder.Seal().As<StructureType>();
        // Ensure that when we changed the type, we have created a new one
        this.Assert(
            changed || result == this,
            "MapFieldTypes: no fields changed but result is a different StructureType" +
            " instance");
        return result;
    }

    /// <summary>
    /// Slices a structure type out of this type.
    /// </summary>
    /// <param name="moduleBuilder">The module builder.</param>
    /// <param name="span">The span to slice.</param>
    /// <returns>The sliced structure type.</returns>
    private TypeValue Slice(ModuleBuilder moduleBuilder, FieldSpan span)
    {
        // If we reach this point we have to create a new structure type
        this.Assert(
            span.HasSpan && span.Span < NumFields,
            $"Slice span out of range: HasSpan={span.HasSpan}, Span={span.Span}," +
            $" NumFields={NumFields}");
        var builder = moduleBuilder.CreateStructureType(span.Span);

        // Slice all field types into the builder
        int index = 0;
        SliceRecursive(ref builder, ref index, span);

        return builder.Seal();
    }

    /// <summary>
    /// Slices a subset of fields recursively.
    /// </summary>
    /// <param name="builder">The target builder to append to.</param>
    /// <param name="index">The current index.</param>
    /// <param name="span">The source span to slice.</param>
    private void SliceRecursive(
        ref Builder builder,
        ref int index,
        in FieldSpan span)
    {
        foreach (var type in DirectFields)
        {
            int numFields = GetNumFields(type);
            var nestedSpan = new FieldSpan(index, numFields);

            // Check whether we can include the whole direct field
            if (nestedSpan.Contains(span))
            {
                // This must be a nested structure
                type.AsNotNullCast<StructureType>().SliceRecursive(
                    ref builder,
                    ref index,
                    span);
            }
            else if (span.Contains(nestedSpan))
            {
                builder.Add(type);
            }

            // Skip parts
            index += numFields;
            if (index >= span.Index + span.Span)
                break;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TypeValue? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var structureBuilder = rewriter.ModuleBuilder.CreateStructureType(Count);
        foreach (var element in DirectFields)
            structureBuilder.Add(rewriter.Rewrite(element));
        return structureBuilder.Seal();
    }

    #endregion

    #region IEnumerable

    /// <summary>
    /// Returns an enumerator to enumerate all fields in this type.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public new Enumerator GetEnumerator() => new(this);

    #endregion

    #region Object

    /// <inheritdoc cref="TypeValue.GetHashCode"/>
    public override int GetHashCode() => _hashCode;

    /// <inheritdoc cref="TypeValue.Equals(object?)"/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override bool Equals(object? obj)
    {
        if (obj is not StructureType structureType ||
            structureType.NumFields != NumFields ||
            structureType.Alignment != Alignment)
        {
            return false;
        }

        // Compare flattened Fields and offsets only. DirectFields preserves
        // high-level nesting which may differ after type lowering (e.g.,
        // LowerViews creates nested {struct{ptr,i64}, i64, i8} while
        // value reconstruction creates flat {ptr, i64, i64, i8}). Both
        // represent the same memory layout and are semantically equivalent.
        for (int i = 0, e = Fields.Length; i < e; ++i)
        {
            if (!Fields[i].Equals(structureType.Fields[i]) ||
                _offsets.Span[i] != structureType._offsets.Span[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns the string "struct".
    /// </summary>
    protected override string ToPrefixString() => "struct";

    /// <inheritdoc cref="TypeValue.ToString()"/>
    public override string ToString()
    {
        var result = new StringBuilder();
        result.Append("struct<");
        if (Fields.Length > 0)
        {
            for (int i = 0, e = Fields.Length; i < e; ++i)
            {
                result.Append(Fields[i].ToString());
                result.Append(" [");
                result.Append(GetOffset(i));
                result.Append(']');
                if (i + 1 < e)
                    result.Append(", ");
            }
        }
        result.Append('>');
        return result.ToString();
    }

    #endregion
}
