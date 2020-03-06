// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Value.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// The base interface of all values.
    /// </summary>
    public interface IValue : INode
    {
        /// <summary>
        /// Returns the current value kind.
        /// </summary>
        ValueKind ValueKind { get; }

        /// <summary>
        /// Returns the associated type information.
        /// </summary>
        TypeNode Type { get; }

        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        BasicBlock BasicBlock { get; }

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        BasicValueType BasicValueType { get; }

        /// <summary>
        /// Returns all associated nodes.
        /// </summary>
        ImmutableArray<ValueReference> Nodes { get; }

        /// <summary>
        /// Returns all associated uses.
        /// </summary>
        UseCollection Uses { get; }

        /// <summary>
        /// Resolves the actual value with respect to
        /// replacement information.
        /// </summary>
        /// <returns>The actual value.</returns>
        Value Resolve();

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The actual value.</returns>
        T ResolveAs<T>() where T : Value;

        /// <summary>
        /// Accepts a value visitor.
        /// </summary>
        /// <typeparam name="T">The type of the visitor.</typeparam>
        /// <param name="visitor">The visitor.</param>
        void Accept<T>(T visitor)
            where T : IValueVisitor;

        /// <summary>
        /// Replaces this value with the given value.
        /// </summary>
        /// <param name="other">The other value.</param>
        void Replace(Value other);
    }

    /// <summary>
    /// Contains extension methods for values.
    /// </summary>
    public static class ValueExtensions
    {
        /// <summary>
        /// Returns true iff the given value is a primitive value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, iff the given value is a primitive value.</returns>
        public static bool IsPrimitive<T>(this T value)
            where T : IValue =>
            value.Resolve() is PrimitiveValue;

        /// <summary>
        /// Returns true iff the given value is an instantiated constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, iff the given value is an instantiated constant value.</returns>
        public static bool IsInstantiatedConstant<T>(this T value)
            where T : IValue =>
            value.Resolve() is ConstantNode;

        /// <summary>
        /// Returns true iff the given value is a device constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, iff the given value is a device constant value.</returns>
        public static bool IsDeviceConstant<T>(this T value)
            where T : IValue =>
            value.Resolve() is DeviceConstantValue;
    }

    /// <summary>
    /// Flags that can be associated with every value.
    /// </summary>
    [Flags]
    public enum ValueFlags : int
    {
        /// <summary>
        /// The default flags.
        /// </summary>
        None,

        /// <summary>
        /// The value cannot be replaced.
        /// </summary>
        NotReplacable = 1 << 0,

        /// <summary>
        /// The value cannot have uses.
        /// </summary>
        NoUses = 1 << 1,
    }

    /// <summary>
    /// Represents a basic intermediate-representation value.
    /// It is the base class for all values in the scope of this IR.
    /// </summary>
    public abstract class Value : Node, IValue, IEquatable<Value>
    {
        #region Constants

        /// <summary>
        /// The default value flags.
        /// </summary>
        public const ValueFlags DefaultFlags = ValueFlags.None;

        #endregion

        #region Nested Types

        /// <summary>
        /// An enumerator for values.
        /// </summary>
        public struct Enumerator : IEnumerator<ValueReference>
        {
            #region Instance

            private ImmutableArray<ValueReference>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new node enumerator.
            /// </summary>
            /// <param name="valueArray">The nodes to iterate over.</param>
            internal Enumerator(ImmutableArray<ValueReference> valueArray)
            {
                enumerator = valueArray.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public ValueReference Current => enumerator.Current.Refresh();

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion


        #region Instance

        /// <summary>
        /// The current node type.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TypeNode type;

        /// <summary>
        /// The collection of all uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        private readonly HashSet<Use> allUses = new HashSet<Use>();

        /// <summary>
        /// Constructs a new value that is marked as replacable.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        protected Value(ValueKind kind, BasicBlock basicBlock, TypeNode initialType)
            : this(kind, basicBlock, initialType, DefaultFlags)
        { }

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="valueKind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        /// <param name="valueFlags">Custom value flags.</param>
        protected Value(
            ValueKind valueKind,
            BasicBlock basicBlock,
            TypeNode initialType,
            ValueFlags valueFlags)
        {
            Debug.Assert(initialType != null, "Invalid initialType");

            ValueKind = valueKind;
            BasicBlock = basicBlock;
            Method = basicBlock?.Method;
            type = initialType;
            Nodes = ImmutableArray<ValueReference>.Empty;

            ValueFlags = valueFlags;
            Replacement = this;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current value kind.
        /// </summary>
        public ValueKind ValueKind { get; }

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public Method Method { get; protected set; }

        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock { get; internal set; }

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public TypeNode Type
        {
            get
            {
                if (type == null)
                    type = UpdateType(Method.Context);
                return type;
            }
        }

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public BasicValueType BasicValueType =>
            Type != null ? Type.BasicValueType : BasicValueType.None;

        /// <summary>
        /// Returns the associated value flags.
        /// </summary>
        public ValueFlags ValueFlags { get; }

        /// <summary>
        /// Returns true if the current value can be replaced.
        /// </summary>
        public bool CanBeReplaced =>
            (ValueFlags & ValueFlags.NotReplacable) != ValueFlags.NotReplacable;

        /// <summary>
        /// Returns true if the current value can have uses.
        /// </summary>
        public bool CanHaveUses =>
            (ValueFlags & ValueFlags.NoUses) != ValueFlags.NoUses;

        /// <summary>
        /// Returns the replacement of this value (if any).
        /// </summary>
        public Value Replacement { get; private set; }

        /// <summary>
        /// Returns true iff the current value has been replaced.
        /// </summary>
        public bool IsReplaced => CanBeReplaced & Replacement != this;

        /// <summary>
        /// Returns all child values.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public ImmutableArray<ValueReference> Nodes { get; private set; }

        /// <summary>
        /// Returns the total number of all associated uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public int AllNumUses => allUses.Count;

        /// <summary>
        /// Returns all current uses (to non-replaced values).
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public UseCollection Uses => new UseCollection(this, allUses);

        /// <summary>
        /// Accesses the child value with the given index.
        /// </summary>
        /// <param name="index">The child-value index.</param>
        /// <returns>The resolved child value.</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ValueReference this[int index] => Nodes[index];

        #endregion

        #region Methods

        /// <summary>
        /// Performs a GC run on this value.
        /// </summary>
        internal void GC()
        {
            // Refresh all value references
            var newNodes = ImmutableArray.CreateBuilder<ValueReference>(Nodes.Length);
            foreach (var node in Nodes)
                newNodes.Add(node.Refresh());
            Nodes = newNodes.MoveToImmutable();

            // Cleanup all uses
            var usesToRemove = new List<Use>(allUses.Count);
            foreach (var use in allUses)
            {
                if (use.Target.IsReplaced)
                    usesToRemove.Add(use);
            }
            foreach (var useToRemove in usesToRemove)
                allUses.Remove(useToRemove);
        }

        /// <summary>
        /// Resolves the first use.
        /// </summary>
        /// <returns>The first use.</returns>
        public Use GetFirstUse()
        {
            using (var enumerator = allUses.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    throw new InvalidOperationException(ErrorMessages.NoUses);
                return enumerator.Current;
            }
        }

        /// <summary>
        /// Resolves the first use as value.
        /// </summary>
        /// <returns>The first use as value.</returns>
        public Value GetFirstUseNode() => GetFirstUse().Resolve();

        /// <summary>
        /// Adds the given use to the use set.
        /// </summary>
        /// <param name="target">The target value.</param>
        /// <param name="useIndex">The use index.</param>
        private void AddUse(Value target, int useIndex)
        {
            Debug.Assert(CanHaveUses, "Value cannot have uses");
            Debug.Assert(target != null, "Invalid target");
            Debug.Assert(useIndex >= 0, "Invalid use index");
            allUses.Add(new Use(target, useIndex));
        }

        /// <summary>
        /// Invalidates the current type and enfores a recomputation
        /// of the current type.
        /// </summary>
        protected void InvalidateType()
        {
            type = null;
        }

        /// <summary>
        /// Computes the current type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        protected abstract TypeNode UpdateType(IRContext context);

        /// <summary>
        /// Accepts a value visitor.
        /// </summary>
        /// <typeparam name="T">The type of the visitor.</typeparam>
        /// <param name="visitor">The visitor.</param>
        public abstract void Accept<T>(T visitor)
            where T : IValueVisitor;

        /// <summary>
        /// Rebuilds the current value in the scope of the given rebuilder.
        /// </summary>
        /// <param name="builder">The builder to use.</param>
        /// <param name="rebuilder">The rebuilder to use.</param>
        /// <returns>The rebuilt value.</returns>
        protected internal abstract Value Rebuild(IRBuilder builder, IRRebuilder rebuilder);

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="nodes">The nested child nodes.</param>
        protected void Seal(ImmutableArray<ValueReference> nodes)
        {
            Nodes = nodes;

            for (int i = 0, e = nodes.Length; i < e; ++i)
            {
                var value = nodes[i].Resolve();
                if (value.CanHaveUses)
                    value.AddUse(this, i);
            }
        }

        /// <summary>
        /// Replaces this value with the given value.
        /// </summary>
        /// <param name="other">The other value.</param>
        public void Replace(Value other)
        {
            Debug.Assert(other != null, "Invalid other node");
            Debug.Assert(CanBeReplaced, "Cannot replace a non-replaceable value");
            Debug.Assert(!IsReplaced, "Cannot replace a replaced value");

            var target = other.Resolve();
            Debug.Assert(target != this, "Invalid replacement cycle");
            Replacement = target;

            if (target.CanHaveUses)
            {
                // Propagate uses
                foreach (var use in allUses)
                    Replacement.AddUse(use.Target, use.Index);
            }

            // Notify nodes
            foreach (var use in allUses)
                use.Target.OnReplacedNode();
        }

        /// <summary>
        /// Invoked when an attached node is replaced.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnReplacedNode()
        {
            InvalidateType();
        }

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <returns>The actual value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value Resolve()
        {
            if (IsReplaced)
                Replacement = Replacement.Resolve();
            return Replacement;
        }

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The actual value.</returns>
        public T ResolveAs<T>() where T : Value => Resolve() as T;

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given value is the same value.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>True, iff the given value is the same value.</returns>
        public bool Equals(Value other) =>
            Equals(other as object);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all child values.
        /// </summary>
        /// <returns>An enumerator to enumerate all child values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Nodes);

        #endregion

        #region Object

        /// <summary>
        /// Returns the argument string (operation args) of this node.
        /// </summary>
        /// <returns>The argument string.</returns>
        protected virtual string ToArgString() => null;

        /// <summary>
        /// Returns the string represetation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public sealed override string ToString()
        {
            var argString = ToArgString();
            if (string.IsNullOrEmpty(argString))
                return ToReferenceString();
            return ToReferenceString() + ": " + argString;
        }

        /// <summary>
        /// Returns true iff the given object is equal to the current value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current value.</returns>
        public override bool Equals(object obj) =>
            base.Equals(obj);

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode() =>
            base.GetHashCode();

        #endregion
    }
}
