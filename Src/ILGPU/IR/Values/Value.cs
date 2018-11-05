// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR
{
    /// <summary>
    /// The base interface of all values.
    /// </summary>
    public interface IValue : INode
    {
#if VERIFICATION
        /// <summary>
        /// Returns the current node generation.
        /// </summary>
        ValueGeneration Generation { get; }
#endif

        /// <summary>
        /// Returns the associated type information.
        /// </summary>
        TypeNode Type { get; }

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
            where T : IValue
        {
            return value.Resolve() is PrimitiveValue;
        }

        /// <summary>
        /// Returns true iff the given value is an instantiated constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, iff the given value is an instantiated constant value.</returns>
        public static bool IsInstantiatedConstant<T>(this T value)
            where T : IValue
        {
            return value.Resolve() is InstantiatedConstantNode;
        }

        /// <summary>
        /// Returns true iff the given value is a device constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, iff the given value is a device constant value.</returns>
        public static bool IsDeviceConstant<T>(this T value)
            where T : IValue
        {
            return value.Resolve() is DeviceConstantValue;
        }

        /// <summary>
        /// Returns true if the given value can be seen as a constant.
        /// </summary>
        /// <param name="valueRef">The given value.</param>
        /// <returns>True if the given value can be seen as a constant.</returns>
        public static bool IsConstant<T>(this T valueRef)
            where T : IValue
        {
            var value = valueRef.Resolve();
            return value.IsInstantiatedConstant() ||
                value is UnifiedValue unifiedValue && unifiedValue.IsConstant ||
                value is MemoryRef memoryRef && memoryRef.Parent.IsInstantiatedConstant();
        }
    }

    /// <summary>
    /// Represents a basic intermediate-representation value.
    /// It is the base class for all values in the scope of this IR.
    /// </summary>
    public abstract class Value : Node, IValue, IEquatable<Value>
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Value replacement;

        /// <summary>
        /// The collection of all uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly HashSet<Use> allUses = new HashSet<Use>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly bool canReplace;

        /// <summary>
        /// Constructs a new value that is marked as replacable.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        protected Value(ValueGeneration generation)
            : this(generation, true)
        { }

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="canBeReplaced">True, iff this value can be replaced.</param>
        protected Value(ValueGeneration generation, bool canBeReplaced)
        {
            Nodes = ImmutableArray<ValueReference>.Empty;
            canReplace = canBeReplaced;

#if VERIFICATION
            Generation = generation;
#endif
        }

        #endregion

        #region Properties

#if VERIFICATION
        /// <summary>
        /// Returns the current node generation.
        /// </summary>
        public ValueGeneration Generation { get; internal set; }
#endif

        /// <summary>
        /// Accesses the internal node type.
        /// </summary>
        protected TypeNode NodeType { get; set; }

        /// <summary>
        /// Returns true iff the current value can be replaced.
        /// </summary>
        public bool CanBeReplaced => canReplace && !IsReplaced;

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public virtual TypeNode Type { get; private set; }

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public BasicValueType BasicValueType =>
            Type != null ? Type.BasicValueType : BasicValueType.None;

        /// <summary>
        /// Returns the replacement of this value (if any).
        /// </summary>
        public Value Replacement => replacement;

        /// <summary>
        /// Returns true iff the current value has been replaced.
        /// </summary>
        public bool IsReplaced => replacement != null;

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
        /// Returns all internal uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        protected HashSet<Use> AllUses => allUses;

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
        /// Resolves the first use.
        /// </summary>
        /// <returns>The first use.</returns>
        public Use GetFirstUse()
        {
            using (var enumerator = allUses.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    throw new InvalidOperationException("No uses");
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
        internal void AddUse(Value target, int useIndex)
        {
            Debug.Assert(target != null, "Invalid target");
            Debug.Assert(useIndex >= 0, "Invalid use index");
            lock (allUses)
                allUses.Add(new Use(target, useIndex));
        }

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
        /// <param name="type">The value type.</param>
        protected void Seal(ImmutableArray<ValueReference> nodes, TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type");

            Type = type;
            Seal(nodes);
        }

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="nodes">The nested child nodes.</param>
        protected virtual void Seal(ImmutableArray<ValueReference> nodes)
        {
            Nodes = nodes;

#if VERIFICATION
            SealNode();

            // Verify generations
            foreach (var node in Nodes)
                Debug.Assert(node.Generation == Generation, "Cannot mix nodes from different generations");
#endif
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
#if VERIFICATION
            Debug.Assert(IsSealed, "Cannot replace a non-sealed value");
            Debug.Assert(Generation == other.Generation, "Cannot replace a value with a value from a different generation");
#endif

            var target = other.Resolve();
            Debug.Assert(target != this, "Invalid replacement cycle");
            replacement = target;

            // Propagate uses
            foreach (var use in allUses)
                replacement.AddUse(use.Target, use.Index);

            // Notify nodes
            foreach (var use in allUses)
                use.Target.OnReplacedNode(use.Index);
        }

        /// <summary>
        /// Invoked when an attached node is replaced.
        /// </summary>
        /// <param name="index">The replacement index.</param>
        protected virtual void OnReplacedNode(int index)
        {
            // Do nothing
        }

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <returns>The actual value.</returns>
        public Value Resolve()
        {
            return IsReplaced ? replacement = Replacement.Resolve() : this;
        }

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The actual value.</returns>
        public T ResolveAs<T>() where T : Value => Resolve() as T;

        /// <summary>
        /// Returns true if this node has attachted replaced nodes.
        /// </summary>
        /// <returns>True, if this node has attached replaced nodes.</returns>
        protected bool HasReplacedNodes()
        {
            foreach (var node in this)
            {
                if (node.DirectTarget.IsReplaced)
                    return true;
            }
            return false;
        }

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
        public ValueEnumerator GetEnumerator()
        {
            return new ValueEnumerator(Nodes);
        }

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

        #endregion
    }

    /// <summary>
    /// The base class for all values that cannot be unified.
    /// </summary>
    public abstract class InstantiatedValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new value that is marked as replacable.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        internal InstantiatedValue(ValueGeneration generation)
            : base(generation)
        { }

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="canBeReplaced">True, iff this value can be replaced.</param>
        internal InstantiatedValue(ValueGeneration generation, bool canBeReplaced)
            : base(generation, canBeReplaced)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Seals this instantiated value.
        /// </summary>
        /// <param name="nodes">The nested child nodes.</param>
        protected override sealed void Seal(ImmutableArray<ValueReference> nodes)
        {
            base.Seal(nodes);

            for (int i = 0, e = nodes.Length; i < e; ++i)
            {
                var value = nodes[i].Resolve();
                value.AddUse(this, i);
            }
        }

        #endregion
    }

    /// <summary>
    /// The base class for all unified values.
    /// </summary>
    public abstract class UnifiedValue : Value
    {
        #region Instance

        private int hashCode;

        /// <summary>
        /// Constructs a new value that is marked as replacable.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        internal UnifiedValue(ValueGeneration generation)
            : base(generation)
        { }

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="canBeReplaced">True, iff this value can be replaced.</param>
        internal UnifiedValue(ValueGeneration generation, bool canBeReplaced)
            : base(generation, canBeReplaced)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this a constant operation.
        /// </summary>
        public bool IsConstant
        {
            get;
            private set;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoked when an attached node is replaced.
        /// </summary>
        /// <param name="index">The replacement index.</param>
        protected override void OnReplacedNode(int index)
        {
            RefreshIsConstant();
        }

        /// <summary>
        /// Refreshes the IsConstant state (if required).
        /// </summary>
        private void RefreshIsConstant()
        {
            if (IsConstant)
                return;

            IsConstant = true;
            foreach (var node in this)
                IsConstant &= node.Resolve().IsConstant();

            if (IsConstant)
            {
                foreach (var use in AllUses)
                {
                    if (use.Resolve() is UnifiedValue target)
                        target.RefreshIsConstant();
                }
            }
        }

        /// <summary>
        /// Seals this unified value.
        /// </summary>
        /// <param name="nodes">The nested child nodes.</param>
        protected override sealed void Seal(ImmutableArray<ValueReference> nodes)
        {
            base.Seal(nodes);

            hashCode = Type.GetHashCode();
            IsConstant = true;
            for (int i = 0, e = nodes.Length; i < e; ++i)
            {
                var value = nodes[i].Resolve();
                value.AddUse(this, i);
                hashCode ^= value.GetHashCode() + 0x66c68586 + (hashCode << 6) + (hashCode >> 2);
                IsConstant &= value.IsConstant();
            }
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the
        /// current unified value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current value.</returns>
        public override bool Equals(object obj)
        {
            if (obj is UnifiedValue value &&
                value.Type == Type &&
                value.hashCode == hashCode)
            {
                var currentNodes = Nodes;
                var otherNodes = value.Nodes;
                if (currentNodes.Length != otherNodes.Length)
                    return false;
                for (int i = 0, e = currentNodes.Length; i < e; ++i)
                {
                    if (currentNodes[i] != otherNodes[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the hash code of this unified value.
        /// </summary>
        /// <returns>The hash code of this unified value.</returns>
        public override int GetHashCode()
        {
            return hashCode;
        }

        #endregion
    }
}
