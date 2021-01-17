// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: StructureType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a structure type.
    /// </summary>
    [SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix")]
    public sealed class StructureType : ObjectType, IEnumerable<(TypeNode, FieldAccess)>
    {
        #region Nested Types

        /// <summary>
        /// A structure type builder.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private TypeFlags typeFlags;
            private readonly ImmutableArray<TypeNode>.Builder fieldsBuilder;
            private readonly ImmutableArray<TypeNode>.Builder allFieldsBuilder;
            private readonly ImmutableArray<int>.Builder offsetsBuilder;

            /// <summary>
            /// Creates a new type builder with the given capacity.
            /// </summary>
            /// <param name="typeContext">The current type context.</param>
            /// <param name="capacity">The initial capacity.</param>
            /// <param name="size">The custom size in bytes (if any).</param>
            internal Builder(
                IRTypeContext typeContext,
                int capacity,
                int size)
            {
                Debug.Assert(capacity >= 0, "Invalid capacity");
                Debug.Assert(size >= 0, "Invalid size");

                typeFlags = TypeFlags.None;

                fieldsBuilder = ImmutableArray.CreateBuilder<TypeNode>(capacity);
                allFieldsBuilder = ImmutableArray.CreateBuilder<TypeNode>(capacity);
                offsetsBuilder = ImmutableArray.CreateBuilder<int>(capacity);
                TypeContext = typeContext;

                ExplicitSize = size;
                Alignment = Offset = 0;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent type context.
            /// </summary>
            public IRTypeContext TypeContext { get; }

            /// <summary>
            /// Returns the explicit size in bytes (if any).
            /// </summary>
            public int ExplicitSize { get; }

            /// <summary>
            /// Returns the number of all fields.
            /// </summary>
            public int Count => allFieldsBuilder.Count;

            /// <summary>
            /// The current offset in bytes.
            /// </summary>
            public int Offset { get; private set; }

            /// <summary>
            /// The current alignment for the underlying type.
            /// </summary>
            public int Alignment { get; private set; }

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
            public TypeNode this[FieldAccess access] => allFieldsBuilder[access.Index];

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given type node to the structure builder.
            /// </summary>
            /// <param name="type">The type node to add.</param>
            public void Add(TypeNode type)
            {
                type.AssertNotNull(type);

                fieldsBuilder.Add(type);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AddInternal(TypeNode type, int offset, int alignment)
            {
                type.Assert(
                    !type.IsStructureType &&
                    offset >= 0);

                allFieldsBuilder.Add(type);
                typeFlags |= type.Flags;

                // Align the next field properly
                Offset = Align(Offset + offset, alignment);
                offsetsBuilder.Add(Offset);
            }

            /// <summary>
            /// Seals this builder and returns a type that corresponds to the type
            /// represented by this builder.
            /// </summary>
            /// <returns>The create type node.</returns>
            public TypeNode Seal()
            {
                // Check for a special case in which we require custom padding elements
                // NB: Prevent empty structures by ensuring we have at least one field
                // that is not padding.
                if (Count == 0 && AlignedSize < Size)
                    Add(TypeContext.Padding8Type.PrimitiveType);

                // If the structure has a single field, apply padding using the size
                // of the initial field. This allows us to fill a fixed buffer with
                // the correct/aligned type.
                if (Count == 1 && AlignedSize < Size)
                {
                    var basicValueType = allFieldsBuilder[0].BasicValueType;
                    var paddingType = TypeContext.GetPaddingType(basicValueType);
                    for (int i = AlignedSize, e = Size; i < e; i += paddingType.Size)
                        Add(paddingType);
                }

                // Ensure that the structure is populated to its required size before
                // we finalize. CAUTION: we have to ignore cases in which this type
                // contains types that will be lowered in future stages.
                if ((typeFlags & TypeFlags.ViewDependent) == TypeFlags.None)
                {
                    for (int i = AlignedSize, e = Size; i < e; ++i)
                        Add(TypeContext.Padding8Type);
                }
                return TypeContext.FinishStructureType(this);
            }

            /// <summary>
            /// Moves the underlying builders to immutable arrays.
            /// </summary>
            /// <param name="types">Direct field types.</param>
            /// <param name="allTypes">All field types.</param>
            /// <param name="offsets">All field offsets.</param>
            /// <param name="remappedFields">Renumbered field indices.</param>
            internal void Seal(
                out ImmutableArray<TypeNode> types,
                out ImmutableArray<TypeNode> allTypes,
                out ImmutableArray<int> offsets,
                out ImmutableArray<int> remappedFields)
            {
                // Calculate a mapping from the original field indices to new
                // field indices, taking into account any padding fields.
                var remapBuilder =
                    ImmutableArray.CreateBuilder<int>(allFieldsBuilder.Capacity);
                for (var i = 0; i < allFieldsBuilder.Count; i++)
                {
                    if (!allFieldsBuilder[i].IsPaddingType)
                        remapBuilder.Add(i);
                }

                // Finalize the structure type.
                types = fieldsBuilder.Count == fieldsBuilder.Capacity
                    ? fieldsBuilder.MoveToImmutable()
                    : fieldsBuilder.ToImmutable();
                allTypes = allFieldsBuilder.Count == allFieldsBuilder.Capacity
                    ? allFieldsBuilder.MoveToImmutable()
                    : allFieldsBuilder.ToImmutable();
                offsets = offsetsBuilder.Count == offsetsBuilder.Capacity
                    ? offsetsBuilder.MoveToImmutable()
                    : offsetsBuilder.ToImmutable();
                remappedFields = remapBuilder.Count == remapBuilder.Capacity
                    ? remapBuilder.MoveToImmutable()
                    : remapBuilder.ToImmutable();
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
        public struct Enumerator : IEnumerator<(TypeNode, FieldAccess)>
        {
            private int index;

            /// <summary>
            /// Constructs a new use enumerator.
            /// </summary>
            /// <param name="type">The structure type.</param>
            internal Enumerator(StructureType type)
            {
                Type = type;
                index = -1;
            }

            /// <summary>
            /// Returns the parent structure type.
            /// </summary>
            public StructureType Type { get; }

            /// <summary>
            /// Returns the current use.
            /// </summary>
            public (TypeNode, FieldAccess) Current =>
                (Type.Fields[index], new FieldAccess(index));

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => ++index < Type.NumFields;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// A readonly collection of all field offsets and paddings.
        /// </summary>
        public readonly struct OffsetCollection :
            IReadOnlyCollection<(FieldAccess, int, int)>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all offsets in the structure type.
            /// </summary>
            /// <remarks>
            /// The tuple contains field access, byte offset and byte padding info.
            /// </remarks>
            public struct Enumerator : IEnumerator<(FieldAccess, int, int)>
            {
                private StructureType.Enumerator enumerator;
                private int currentPadding;
                private int offset;

                /// <summary>
                /// Constructs a new use enumerator.
                /// </summary>
                /// <param name="type">The structure type.</param>
                internal Enumerator(StructureType type)
                {
                    enumerator = type.GetEnumerator();
                    currentPadding = offset = 0;
                }

                /// <summary>
                /// Returns the parent structure type.
                /// </summary>
                public StructureType Type => enumerator.Type;

                /// <summary>
                /// Returns the current use.
                /// </summary>
                public (FieldAccess, int, int) Current
                {
                    get
                    {
                        var (_, access) = enumerator.Current;
                        return (access, Type.GetOffset(access), currentPadding);
                    }
                }

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    // Adjust current offset
                    if (!enumerator.MoveNext())
                        return false;

                    // Align the current offset
                    var (type, access) = enumerator.Current;

                    // Compute the current offsets
                    int alignedOffset = Align(offset, type.Alignment);
                    int fieldOffset = Type.GetOffset(access);
                    currentPadding = fieldOffset - alignedOffset;
                    Debug.Assert(currentPadding >= 0, "Invalid padding");

                    // Adjust the next offset
                    offset = fieldOffset + type.Size;
                    return true;
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new offset collection.
            /// </summary>
            /// <param name="parent">The parent structure type.</param>
            internal OffsetCollection(StructureType parent)
            {
                Parent = parent;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent structure type.
            /// </summary>
            public StructureType Parent { get; }

            /// <summary>
            /// Returns the number of offsets.
            /// </summary>
            public int Count => Parent.NumFields;

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns an enumerator to enumerate all offsets in the parent type.
            /// </summary>
            /// <returns>The enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(Parent);

            /// <summary>
            /// Returns an enumerator to enumerate all offsets in the parent type.
            /// </summary>
            /// <returns>The enumerator.</returns>
            IEnumerator<(FieldAccess, int, int)>
                IEnumerable<(FieldAccess, int, int)>.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Returns an enumerator to enumerate all offsets in the parent type.
            /// </summary>
            /// <returns>The enumerator.</returns>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Contains all vectorizable field ranges in the scope of its parent type.
        /// </summary>
        public readonly struct VectorizableFieldCollection
        {
            #region Nested Types

            /// <summary>
            /// Represents a vectorizable sub range in the scope of a structure type.
            /// </summary>
            public struct Entry
            {
                #region Instance

                /// <summary>
                /// Constructs a new entry.
                /// </summary>
                internal Entry(
                    TypeNode type,
                    int index,
                    int offset,
                    int count = 1)
                {
                    Type = type;
                    Index = index;
                    Count = count;
                    Offset = offset;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the associated type.
                /// </summary>
                public TypeNode Type { get; }

                /// <summary>
                /// Returns the start index within the parent structure.
                /// </summary>
                public int Index { get; }

                /// <summary>
                /// Returns the number of fields.
                /// </summary>
                public int Count { readonly get; private set; }

                /// <summary>
                /// Returns the base offset in bytes from the beginning of the field.
                /// </summary>
                public int Offset { get; }

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
                internal void Split(out Entry first, out Entry second)
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
                internal void AddField() => ++Count;

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
                internal readonly bool CanBeAligned(StructureType parentType)
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

            private readonly List<Entry> ranges;

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
                ranges = new List<Entry>(structureType.NumFields);

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
                    if (current.Type != nextType ||
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
            private void RegisterRange(
                StructureType structureType,
                in Entry entry)
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
                            ranges.Add(newEntry);
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
            public readonly int Count => ranges.Count;

            /// <summary>
            /// Returns the i-th entry.
            /// </summary>
            /// <param name="index">The entry index.</param>
            /// <returns>The i-th vector range entry.</returns>
            public readonly Entry this[int index] => ranges[index];

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Gets the number of fields of the given type.
        /// </summary>
        /// <param name="typeNode">The type.</param>
        /// <returns>The number of nested fields (or 1).</returns>
        public static int GetNumFields(TypeNode typeNode) =>
            typeNode is StructureType structureType
            ? structureType.NumFields
            : 1;

        #endregion

        #region Instance

        /// <summary>
        /// Caches the internal hash code of all child nodes.
        /// </summary>
        private readonly int hashCode = 0;

        /// <summary>
        /// All underlying byte offsets.
        /// </summary>
        private readonly ImmutableArray<int> offsets;

        /// <summary>
        /// Maps the original field index to the index of the rebuilt field.
        /// </summary>
        private readonly ImmutableArray<int> remappedFields;

        /// <summary>
        /// Constructs a new object type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="builder">The current structure builder.</param>
        internal StructureType(IRTypeContext typeContext, in Builder builder)
            : base(typeContext)
        {
            Alignment = builder.Alignment;
            Size = builder.Size;

            builder.Seal(
                out var fields,
                out var allFields,
                out offsets,
                out remappedFields);
            DirectFields = fields;
            Fields = allFields;

            // Update flags and init hash code
            for (int i = 0, e = NumFields; i < e; ++i)
            {
                var type = Fields[i];
                hashCode ^= type.GetHashCode() ^ offsets[i];
                AddFlags(type.Flags);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the high-level fields stored in this structure type.
        /// </summary>
        public ImmutableArray<TypeNode> DirectFields { get; }

        /// <summary>
        /// Returns all associated fields.
        /// </summary>
        public ImmutableArray<TypeNode> Fields { get; }

        /// <summary>
        /// Returns a readonly collection of all field offsets.
        /// </summary>
        public OffsetCollection Offsets => new OffsetCollection(this);

        /// <summary>
        /// Returns the number of associated fields.
        /// </summary>
        public int NumFields => Fields.Length;

        /// <summary>
        /// Returns the field type that corresponds to the given field access.
        /// </summary>
        /// <param name="fieldAccess">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public TypeNode this[FieldAccess fieldAccess] => Fields[fieldAccess.Index];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a readonly collection of all vectorized field configurations that
        /// do not exceed the maximum size in bytes.
        /// </summary>
        /// <param name="maxSizeInBytes">The maximum vector size in bytes.</param>
        /// <returns>The vectorizable field collection.</returns>
        public VectorizableFieldCollection GetVectorizableFields(int maxSizeInBytes) =>
            new VectorizableFieldCollection(this, maxSizeInBytes);

        /// <summary>
        /// Gets a specific field offset in bytes from the beginning of the structure.
        /// </summary>
        /// <param name="fieldAccess">The field reference.</param>
        /// <returns>The field offset in bytes.</returns>
        public int GetOffset(FieldAccess fieldAccess) => offsets[fieldAccess.Index];

        /// <summary>
        /// Gets the remapped field index corresponding to the original structure.
        /// </summary>
        /// <param name="fieldIndex">The field index.</param>
        /// <returns>The adjusted field index.</returns>
        public int RemapFieldIndex(int fieldIndex) => remappedFields[fieldIndex];

        /// <summary>
        /// Gets a nested type that corresponds to the given span.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <param name="span">The span to slice.</param>
        /// <returns>The nested type.</returns>
        public TypeNode Get<TTypeContext>(TTypeContext typeContext, FieldSpan span)
            where TTypeContext : IIRTypeContext =>
            !span.HasSpan
            ? this[span.Access]
            : span.Index == 0 && span.Span == NumFields
                ? this
                : Slice(typeContext, span);

        /// <summary>
        /// Converts all field types using the type converter provided.
        /// </summary>
        /// <typeparam name="TTypeContext">The type context to use.</typeparam>
        /// <typeparam name="TTypeConverter">The type converter to use.</typeparam>
        /// <param name="typeContext">The type context instance to use.</param>
        /// <param name="typeConverter">The type converter instance to use.</param>
        /// <returns></returns>
        public StructureType ConvertFieldTypes<TTypeContext, TTypeConverter>(
            TTypeContext typeContext,
            TTypeConverter typeConverter)
            where TTypeContext : IIRTypeContext
            where TTypeConverter : ITypeConverter<TypeNode>
        {
            // Iterate over all direct fields to preserve the high-level layout
            var builder = typeContext.CreateStructureType(NumFields);
            bool changed = false;
            foreach (var type in DirectFields)
            {
                // Convert type and append it
                var convertedType = typeConverter.ConvertType(typeContext, type);
                builder.Add(convertedType);
                changed |= convertedType != type;
            }

            // Ensure that we did not lose any fields
            this.Assert(builder.Count >= NumFields);

            // Create final structure type
            var result = builder.Seal().As<StructureType>(this);
            // Ensure that when we changed the type, we have created a new one
            this.Assert(changed || result == this);
            return result;
        }

        /// <summary>
        /// Slices a structure type out of this type.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <param name="span">The span to slice.</param>
        /// <returns>The sliced structure type.</returns>
        private TypeNode Slice<TTypeContext>(
            TTypeContext typeContext,
            FieldSpan span)
            where TTypeContext : IIRTypeContext
        {
            // If we reach this point we have to create a new structure type
            this.Assert(span.HasSpan && span.Span < NumFields);
            var builder = typeContext.CreateStructureType(span.Span);

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
                if (span.Contains(nestedSpan))
                {
                    builder.Add(type);
                }
                else if (nestedSpan.Contains(span))
                {
                    // This must be a nested structure
                    (type as StructureType).SliceRecursive(
                        ref builder,
                        ref index,
                        span);
                }

                // Skip parts
                index += numFields;
                if (index >= span.Index + span.Span)
                    break;
            }
        }

        /// <summary>
        /// Creates a managed type that corresponds to this structure type.
        /// </summary>
        protected override Type GetManagedType()
        {
            var typeBuilder = RuntimeSystem.Instance.DefineRuntimeStruct();
            int index = 0;
            foreach (var type in DirectFields)
            {
                typeBuilder.DefineField(
                    "Field" + index++,
                    type.ManagedType,
                    FieldAttributes.Public);

            }
            return typeBuilder.CreateType();
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all fields in this type.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns an enumerator to enumerate all fields in this type.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<(TypeNode, FieldAccess)>
            IEnumerable<(TypeNode, FieldAccess)>.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerate all fields in this type.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Struct";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() => hashCode;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (!(obj is StructureType structureType) ||
                structureType.NumFields != NumFields)
            {
                return false;
            }

            for (int i = 0, e = Fields.Length; i < e; ++i)
            {
                if (Fields[i] != structureType.Fields[i] ||
                    offsets[i] != structureType.offsets[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary cref="TypeNode.ToString()"/>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append(ToPrefixString());
            result.Append('<');
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
}
