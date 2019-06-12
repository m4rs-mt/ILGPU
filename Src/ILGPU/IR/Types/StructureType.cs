// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: StructureType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a structure type.
    /// </summary>
    public sealed class StructureType : ObjectType
    {
        #region Nested Types

        /// <summary>
        /// A reference to a scalar structure field.
        /// These values can be used during CPS construction to reference
        /// all scalar fields within the scope of a structure value.
        /// </summary>
        public readonly struct FieldRef : IEquatable<FieldRef>
        {
            #region Instance

            /// <summary>
            /// The cached hash code.
            /// </summary>
            private readonly int hashCode;

            /// <summary>
            /// Constructs a new direct reference to the given node.
            /// </summary>
            /// <param name="source">The main source.</param>
            public FieldRef(Value source)
                : this(source, ImmutableArray<int>.Empty)
            { }

            /// <summary>
            /// Constructs a new direct reference to the given node.
            /// </summary>
            /// <param name="source">The main source.</param>
            /// <param name="accessChain">The indices of this reference.</param>
            public FieldRef(Value source, ImmutableArray<int> accessChain)
            {
                Source = source;
                AccessChain = accessChain;

                hashCode = accessChain.Length ^ source.GetHashCode();
                foreach (var chainEntry in accessChain)
                    hashCode ^= chainEntry.GetHashCode();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns true iff this field reference points to a valid field.
            /// </summary>
            public bool IsValid => Source != null;

            /// <summary>
            /// Returns the source node (the main structure value).
            /// </summary>
            public Value Source { get; }

            /// <summary>
            /// Returns the access chain element for the given index.
            /// </summary>
            /// <param name="index">The access chain index.</param>
            /// <returns>The resolved chain element.</returns>
            public int this[int index] => AccessChain[index];

            /// <summary>
            /// Returns the number of chain elements.
            /// </summary>
            public int ChainLength => AccessChain.Length;

            /// <summary>
            /// Returns the list of index elements.
            /// </summary>
            public ImmutableArray<int> AccessChain { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Realizes an additional access operation to the
            /// given field index.
            /// </summary>
            /// <param name="fieldIndex">The next field index.</param>
            /// <returns>The extended field reference.</returns>
            public FieldRef Access(int fieldIndex)
            {
                return new FieldRef(Source, AccessChain.Add(fieldIndex));
            }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true iff the given field ref is equal to the current one.
            /// </summary>
            /// <param name="other">The other field reference.</param>
            /// <returns>True, iff the given field ref is equal to the current one.</returns>
            public bool Equals(FieldRef other)
            {
                if (Source != other.Source)
                    return false;
                var chain = AccessChain;
                var otherChain = other.AccessChain;
                if (chain.Length != otherChain.Length)
                    return false;
                for (int i = 0, e = chain.Length; i < e; ++i)
                {
                    if (chain[i] != otherChain[i])
                        return false;
                }
                return true;
            }

            #endregion

            #region Object

            /// <summary>
            /// Returns true iff the given object is equal to the current one.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>True, iff the given field ref is equal to the current one.</returns>
            public override bool Equals(object obj)
            {
                if (obj is FieldRef other)
                    return Equals(other);
                return false;
            }

            /// <summary>
            /// Returns the hash code of this field reference.
            /// </summary>
            /// <returns>The hash code of this field reference.</returns>
            public override int GetHashCode() => hashCode;

            /// <summary>
            /// Returns the string representation of this field reference.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                var result = new StringBuilder();
                result.Append(Source.ToReferenceString());
                foreach (var entry in AccessChain)
                {
                    result.Append('[');
                    result.Append(entry);
                    result.Append(']');
                }
                return result.ToString();
            }

            #endregion

            #region Operators

            /// <summary>
            /// Returns true iff the first and second field ref are the same.
            /// </summary>
            /// <param name="first">The first field ref.</param>
            /// <param name="second">The second field ref.</param>
            /// <returns>True, iff the first and second field ref are the same.</returns>
            public static bool operator ==(FieldRef first, FieldRef second) => first.Equals(second);

            /// <summary>
            /// Returns true iff the first and second field ref are not the same.
            /// </summary>
            /// <param name="first">The first field ref.</param>
            /// <param name="second">The second field ref.</param>
            /// <returns>True, iff the first and second field ref are not the same.</returns>
            public static bool operator !=(FieldRef first, FieldRef second) => !first.Equals(second);

            #endregion
        }

        /// <summary>
        /// Represents an action that is applied to every scalar field.
        /// </summary>
        public interface IBasicFieldAction
        {
            /// <summary>
            /// Applies this action to the given scalar field.
            /// </summary>
            /// <param name="fieldType">The current field type.</param>
            /// <param name="absoluteFieldIndex">
            /// The absolute field index in the scope of the main parent structure.
            /// </param>
            void Apply(TypeNode fieldType, int absoluteFieldIndex);
        }

        /// <summary>
        /// Represents an action that is applied to every scalar field.
        /// </summary>
        public interface IFieldAction<T>
        {
            /// <summary>
            /// Resolves an internal field value for further processing.
            /// This method allows to encapsulate a temporary processing
            /// state for further operations.
            /// </summary>
            /// <param name="parentValue">The parent value.</param>
            /// <param name="structureType">The current structure type.</param>
            /// <param name="fieldIndex">The current field index (within the type info object).</param>
            /// <returns>The resolved value.</returns>
            T GetFieldValue(
                T parentValue,
                StructureType structureType,
                int fieldIndex);

            /// <summary>
            /// Applies this action to the given scalar field.
            /// </summary>
            /// <param name="fieldValue">The current field value that was resolved using the
            /// <see cref="GetFieldValue(T, StructureType, int)"/> method.</param>
            /// <param name="structureType">The current structure type.</param>
            /// <param name="fieldIndex">The current field index (within the type info object).</param>
            void Apply(
                T fieldValue,
                StructureType structureType,
                int fieldIndex);
        }

        /// <summary>
        /// Represents an action that traverses the main structure and constructs
        /// field references and applies the action to every scalar field.
        /// </summary>
        /// <typeparam name="T">The temporary value.</typeparam>
        public interface IFieldRefAction<T>
        {
            /// <summary>
            /// Resolves an internal field value for further processing.
            /// This method allows to encapsulate a temporary processing
            /// state for further operations.
            /// </summary>
            /// <param name="fieldRef">The reference to the current field.</param>
            /// <param name="parentValue">The parent value.</param>
            /// <param name="structureType">The current structure type.</param>
            /// <param name="fieldIndex">The current field index (within the type info object).</param>
            /// <returns>The resolved value.</returns>
            T GetFieldValue(
                in FieldRef fieldRef,
                T parentValue,
                StructureType structureType,
                int fieldIndex);

            /// <summary>
            /// Applies this action to the given scalar field.
            /// </summary>
            /// <param name="fieldRef">The reference to the current field.</param>
            /// <param name="fieldValue">The current field value that was resolved using the
            /// <see cref="GetFieldValue(in FieldRef, T, StructureType, int)"/> method.</param>
            /// <param name="structureType">The current structure type.</param>
            /// <param name="fieldIndex">The current field index (within the type info object).</param>
            void Apply(
                in FieldRef fieldRef,
                T fieldValue,
                StructureType structureType,
                int fieldIndex);
        }

        struct NumFieldAction : IBasicFieldAction
        {
            public int NumFields { get; private set; }

            /// <summary cref="IBasicFieldAction.Apply(TypeNode, int)"/>
            public void Apply(TypeNode fieldType, int absoluteFieldIndex) => ++NumFields;
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
        /// Performs the given <see cref="IBasicFieldAction"/>
        /// by recursively applying the action to all (nested) structure fields.
        /// </summary>
        /// <typeparam name="TAction">The action type.</typeparam>
        /// <param name="structureType">The current type information.</param>
        /// <param name="action">The action to apply.</param>
        public static void ForEachField<TAction>(
            StructureType structureType,
            ref TAction action)
            where TAction : IBasicFieldAction
        {
            for (int i = 0, e = structureType.NumFields; i < e; ++i)
            {
                if (structureType.Fields[i] is StructureType childType)
                    ForEachField(childType, ref action);
                else
                    action.Apply(structureType, i);
            }
        }

        /// <summary>
        /// Performs the given <see cref="IFieldAction{T}"/>
        /// by recursively applying the action to all (nested) structure fields.
        /// </summary>
        /// <typeparam name="TAction">The action type.</typeparam>
        /// <typeparam name="T">The custom intermediate value type.</typeparam>
        /// <param name="structureType">The current type information.</param>
        /// <param name="value">The custom value to pass to the action.</param>
        /// <param name="action">The action to apply.</param>
        public static void ForEachField<TAction, T>(
            StructureType structureType,
            T value,
            ref TAction action)
            where TAction : IFieldAction<T>
        {
            for (int i = 0, e = structureType.NumFields; i < e; ++i)
            {
                var fieldValue = action.GetFieldValue(value, structureType, i);
                if (structureType.Fields[i] is StructureType childType)
                    ForEachField(childType, fieldValue, ref action);
                else
                    action.Apply(fieldValue, structureType, i);
            }
        }

        /// <summary>
        /// Performs the given <see cref="IFieldRefAction{T}"/>
        /// by recursively applying the action to all (nested) structure fields.
        /// </summary>
        /// <typeparam name="TAction">The action type.</typeparam>
        /// <typeparam name="T">The custom intermediate value type.</typeparam>
        /// <param name="currentRef">The current field reference to the root element.</param>
        /// <param name="structureType">The current type information.</param>
        /// <param name="value">The custom value to pass to the action.</param>
        /// <param name="action">The action to apply.</param>
        public static void ForEachField<TAction, T>(
            StructureType structureType,
            in FieldRef currentRef,
            T value,
            ref TAction action)
            where TAction : IFieldRefAction<T>
        {
            for (int i = 0, e = structureType.NumFields; i < e; ++i)
            {
                var fieldRef = currentRef.Access(i);
                var fieldValue = action.GetFieldValue(fieldRef, value, structureType, i);
                if (structureType.Fields[i] is StructureType childType)
                    ForEachField(childType, fieldRef, fieldValue, ref action);
                else
                    action.Apply(fieldRef, fieldValue, structureType, i);
            }
        }

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
        private StructureType(
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type source)
            : base(source)
        {
            Fields = fieldTypes;
            Names = fieldNames;

            hashCode = 0;
            foreach (var type in fieldTypes)
                hashCode ^= type.GetHashCode();
        }

        /// <summary>
        /// Constructs a new object type.
        /// </summary>
        /// <param name="baseType">The underlying base class.</param>
        /// <param name="fieldTypes">The field types.</param>
        /// <param name="fieldNames">The field names.</param>
        /// <param name="source">The original source type (or null).</param>
        internal StructureType(
            StructureType baseType,
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type source)
            : this(fieldTypes, fieldNames, source)
        {
            Debug.Assert(baseType != null, "Invalid base class");
            BaseType = baseType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this is the base object class.
        /// </summary>
        public bool IsObject => BaseType == null;

        /// <summary>
        /// Returns the underlying parent base class.
        /// </summary>
        public StructureType BaseType { get; }

        /// <summary>
        /// Returns the associated fields.
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

        #endregion

        #region Methods

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

        /// <summary>
        /// Resolves the number of scalar fields.
        /// </summary>
        /// <returns>The number of scalar fields.</returns>
        public int ResolveNumScalarFields()
        {
            var action = new NumFieldAction();
            ForEachField(this, ref action);
            return action.NumFields;
        }

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary>
        /// Performs the given <see cref="IFieldAction{T}"/>
        /// by recursively applying the action to all (nested) structure fields.
        /// </summary>
        /// <typeparam name="TAction">The action type.</typeparam>
        /// <typeparam name="T">The custom intermediate value type.</typeparam>
        /// <param name="value">The custom value to pass to the action.</param>
        /// <param name="action">The action to apply.</param>
        public void ForEachField<TAction, T>(
            T value,
            ref TAction action)
            where TAction : IFieldAction<T> =>
            ForEachField(this, value, ref action);

        /// <summary>
        /// Performs the given <see cref="IFieldRefAction{T}"/>
        /// by recursively applying the action to all (nested) structure fields.
        /// </summary>
        /// <typeparam name="TAction">The action type.</typeparam>
        /// <typeparam name="T">The custom intermediate value type.</typeparam>
        /// <param name="currentRef">The current field reference to the root element.</param>
        /// <param name="value">The custom value to pass to the action.</param>
        /// <param name="action">The action to apply.</param>
        public void ForEachField<TAction, T>(
            in FieldRef currentRef,
            T value,
            ref TAction action)
            where TAction : IFieldRefAction<T> =>
            ForEachField(this, currentRef, value, ref action);

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
                structureType.BaseType != BaseType ||
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
