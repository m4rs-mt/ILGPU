// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: StructureType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        /// Returns an enumerator to enumerate all nested fields in the structure type.
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

        #endregion

        #region Static

        /// <summary>
        /// Represents the base object class of all objects.
        /// </summary>
        public static StructureType Root { get; } = new StructureType(
            ImmutableArray<TypeNode>.Empty,
            ImmutableArray<string>.Empty,
            typeof(object));

        /// <summary>
        /// Gets the number of fields of the given type.
        /// </summary>
        /// <param name="typeNode">The type.</param>
        /// <returns>The number of nested fields (or 1).</returns>
        public static int GetNumFields(TypeNode typeNode) =>
            typeNode is StructureType structureType ?
            structureType.NumFields :
            1;

        #endregion

        #region Instance

        /// <summary>
        /// Caches the internal hash code of all child nodes.
        /// </summary>
        private readonly int hashCode;

        /// <summary>
        /// Constructs a new object type.
        /// </summary>
        /// <param name="fieldTypes">The field types.</param>
        /// <param name="fieldNames">The field names.</param>
        /// <param name="source">The original source type (or null).</param>
        internal StructureType(
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type source)
            : base(source)
        {
            Fields = fieldTypes;
            Names = fieldNames;

            hashCode = 0;
            foreach (var type in fieldTypes)
            {
                Debug.Assert(!(type is StructureType), "Invalid nested structure type");
                hashCode ^= type.GetHashCode();
                AddFlags(type.Flags);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns all associated fields.
        /// </summary>
        public ImmutableArray<TypeNode> Fields { get; }

        /// <summary>
        /// Returns the number of associated fields.
        /// </summary>
        public int NumFields => Fields.Length;

        /// <summary>
        /// Returns the associated name information.
        /// </summary>
        internal ImmutableArray<string> Names { get; }

        /// <summary>
        /// Returns the field type that corresponds to the given field access.
        /// </summary>
        /// <param name="fieldAccess">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public TypeNode this[FieldAccess fieldAccess] => Fields[fieldAccess.Index];

        #endregion

        #region Methods

        /// <summary>
        /// Gets a nested type that corresponds to the given span.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <param name="span">The span to slice.</param>
        /// <returns>The nested type.</returns>
        public TypeNode Get<TTypeContext>(TTypeContext typeContext, FieldSpan span)
            where TTypeContext : IIRTypeContext
        {
            if (!span.HasSpan)
                return this[span.Access];
            else
                return Slice(typeContext, span);
        }

        /// <summary>
        /// Slices a structure type out of this type.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <param name="span">The span to slice.</param>
        /// <returns>The sliced structure type.</returns>
        public TypeNode Slice<TTypeContext>(TTypeContext typeContext, FieldSpan span)
            where TTypeContext : IIRTypeContext =>
            typeContext.CreateStructureType(Slice(span));

        /// <summary>
        /// Slices the specified field types.
        /// </summary>
        /// <param name="span">The span to slice.</param>
        /// <returns>The sliced field types.</returns>
        public ImmutableArray<TypeNode> Slice(FieldSpan span)
        {
            var fieldTypes = span.CreateFieldTypeBuilder();
            for (int i = 0; i < span.Span; ++i)
                fieldTypes.Add(this[span.Index + i]);
            return fieldTypes.MoveToImmutable();
        }

        /// <summary>
        /// Creates a new immutable array builder that has a sufficient capacity for all values.
        /// </summary>
        /// <returns>The created field builder.</returns>
        public ImmutableArray<TypeNode>.Builder CreateFieldTypeBuilder() =>
            ImmutableArray.CreateBuilder<TypeNode>(NumFields);

        /// <summary>
        /// Creates a new immutable array builder that has a sufficient capacity for all values.
        /// </summary>
        /// <returns>The created field builder.</returns>
        public ImmutableArray<ValueReference>.Builder CreateFieldBuilder() =>
            ImmutableArray.CreateBuilder<ValueReference>(NumFields);

        /// <summary>
        /// Returns the name of the specified child.
        /// </summary>
        /// <param name="childIndex">The child index.</param>
        /// <returns>The name of the specified child.</returns>
        public string GetName(int childIndex)
        {
            if (childIndex < 0 || childIndex >= Fields.Length)
                throw new ArgumentOutOfRangeException(nameof(childIndex));
            if (childIndex < Names.Length)
                return Names[childIndex];
            return string.Empty;
        }

        /// <summary cref="TypeNode.TryResolveManagedType(out Type)"/>
        public override bool TryResolveManagedType(out Type type)
        {
            type = Source;
            return type != null;
        }

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

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
        IEnumerator<(TypeNode, FieldAccess)> IEnumerable<(TypeNode, FieldAccess)>.GetEnumerator() =>
            GetEnumerator();

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
                return false;
            for (int i = 0, e = Fields.Length; i < e; ++i)
            {
                if (Fields[i] != structureType.Fields[i])
                    return false;
            }
            return true;
        }

        /// <summary cref="TypeNode.ToString()"/>
        public override string ToString()
        {
            if (Source != null)
                return Source.GetStringRepresentation();

            var result = new StringBuilder();
            result.Append(ToPrefixString());
            result.Append('<');

            if (Fields.Length > 0)
            {
                for (int i = 0, e = Fields.Length; i < e; ++i)
                {
                    result.Append(Fields[i].ToString());
                    var name = GetName(i);
                    if (!string.IsNullOrEmpty(name))
                    {
                        result.Append(' ');
                        result.Append(name);
                    }
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
