// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Value.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UseList = ILGPU.Util.InlineList<ILGPU.IR.Values.Use>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

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
        ReadOnlySpan<ValueReference> Nodes { get; }

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
        /// Returns true if the given value is a primitive value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, if the given value is a primitive value.</returns>
        public static bool IsPrimitive<T>(this T value)
            where T : IValue =>
            value.Resolve() is PrimitiveValue;

        /// <summary>
        /// Returns true if the given value is a primitive value with the specified raw
        /// value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="rawValue">The expected raw value.</param>
        /// <returns>
        /// True, if the given value is a primitive value with the specified raw value.
        /// </returns>
        public static bool IsPrimitive<T>(this T value, long rawValue)
            where T : IValue =>
            value.Resolve() is PrimitiveValue primitive &&
            primitive.RawValue == rawValue;

        /// <summary>
        /// Returns true if the given value is an instantiated constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>
        /// True, if the given value is an instantiated constant value.
        /// </returns>
        public static bool IsInstantiatedConstant<T>(this T value)
            where T : IValue =>
            value.Resolve() is ConstantNode;

        /// <summary>
        /// Returns true if the given value is a device constant value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>True, if the given value is a device constant value.</returns>
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

        /// <summary>
        /// Static type
        /// </summary>
        StaticType = 1 << 2,

        /// <summary>
        /// The value has been sealed.
        /// </summary>
        IsSealed = 1 << 3,
    }

    /// <summary>
    /// A general value initializer.
    /// </summary>
    public readonly struct ValueInitializer : ILocation
    {
        #region Instance

        /// <summary>
        /// Constructs a new value initializer.
        /// </summary>
        /// <param name="context">The context reference.</param>
        /// <param name="parent">The associated parent.</param>
        /// <param name="location">The current location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueInitializer(
            IRBaseContext context,
            ValueParent parent,
            Location location)
        {
            // Enforce a valid location in all cases
            Locations.AssertNotNull(location, location);

            Context = context;
            Parent = parent;
            Location = location;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context reference.
        /// </summary>
        public IRBaseContext Context { get; }

        /// <summary>
        /// Returns the associated parent.
        /// </summary>
        public ValueParent Parent { get; }

        /// <summary>
        /// Returns the associated location.
        /// </summary>
        public Location Location { get; }

        #endregion

        #region ILocation

        /// <summary>
        /// Formats an error message to include specific location information.
        /// </summary>
        string ILocation.FormatErrorMessage(string message) =>
            Location.FormatErrorMessage(message);

        #endregion
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

        #region Instance

        /// <summary>
        /// The current parent container.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ValueParent parent;

        /// <summary>
        /// The current node type.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TypeNode type;

        /// <summary>
        /// The list of all values.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ValueList values;

        /// <summary>
        /// The collection of all uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private UseList uses;

        /// <summary>
        /// Constructs a new value that is marked as replaceable.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Value(in ValueInitializer initializer)
            : this(initializer, ValueFlags.None)
        { }

        /// <summary>
        /// Constructs a new value that is marked as replaceable.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Value(
            in ValueInitializer initializer,
            TypeNode staticType)
            : this(initializer, ValueFlags.None, staticType)
        { }

        /// <summary>
        /// Constructs a new value that is marked as replaceable.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="valueFlags">The value flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Value(
            in ValueInitializer initializer,
            ValueFlags valueFlags)
            : this(initializer, valueFlags, null)
        { }

        /// <summary>
        /// Constructs a new value that is marked as replaceable.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="valueFlags">The value flags.</param>
        /// <param name="staticType">The static type (if any).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Value(
            in ValueInitializer initializer,
            ValueFlags valueFlags,
            TypeNode staticType)
            : base(initializer.Location)
        {
            parent = initializer.Parent;
            type = staticType;
            values = ValueList.Empty;
            uses = UseList.Empty;

            if (staticType != null)
                valueFlags |= ValueFlags.StaticType;
            ValueFlags = valueFlags;
            Replacement = this;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current value kind.
        /// </summary>
        public abstract ValueKind ValueKind { get; }

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public Method Method =>
            parent is BasicBlock basicBlock
                ? basicBlock.Method
                : parent as Method;

        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock
        {
            get => parent as BasicBlock;
            internal set
            {
                this.Assert(parent.IsBasicBlock);
                parent = value;
            }
        }

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public TypeNode Type
        {
            get
            {
                if (type == null)
                {
                    this.Assert(!HasStaticType);
                    type = ComputeType(new ValueInitializer(
                        Method.BaseContext,
                        parent,
                        Location));
                }

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
        public ValueFlags ValueFlags { get; private set; }

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
        /// Returns true if the current value has a static type.
        /// </summary>
        public bool HasStaticType =>
            (ValueFlags & ValueFlags.StaticType) != ValueFlags.None;

        /// <summary>
        /// Returns true if the current value has been sealed.
        /// </summary>
        public bool IsSealed =>
            (ValueFlags & ValueFlags.IsSealed) != ValueFlags.None;

        /// <summary>
        /// Returns the replacement of this value (if any).
        /// </summary>
        public Value Replacement { get; private set; }

        /// <summary>
        /// Returns true if the current value has been replaced.
        /// </summary>
        public bool IsReplaced => CanBeReplaced & Replacement != this;

        /// <summary>
        /// Returns all child values.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public ReadOnlySpan<ValueReference> Nodes => values;

        /// <summary>
        /// Returns the number of child values.
        /// </summary>
        public int Count => values.Count;

        /// <summary>
        /// Returns the total number of all associated uses.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public int AllNumUses => uses.Count;

        /// <summary>
        /// Returns all current uses (to non-replaced values).
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public UseCollection Uses => new UseCollection(this, uses);

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
            var newNodes = ValueList.Create(values.Count);
            foreach (var node in Nodes)
                newNodes.Add(node.Refresh());
            newNodes.MoveTo(ref values);

            // Cleanup all uses
            var newUses = UseList.Create(uses.Count);
            foreach (var use in uses)
            {
                if (!use.Target.IsReplaced)
                    newUses.Add(use);
            }

            newUses.MoveTo(ref uses);
        }

        /// <summary>
        /// Resolves the first use.
        /// </summary>
        /// <returns>The first use.</returns>
        public Use GetFirstUse()
        {
            var enumerator = uses.GetEnumerator();
            return enumerator.MoveNext()
                ? enumerator.Current
                : throw new InvalidOperationException(ErrorMessages.NoUses);
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
            this.AssertNotNull(target);
            this.Assert(CanHaveUses && useIndex >= 0);
            uses.Add(new Use(target, useIndex));
        }

        /// <summary>
        /// Invalidates the current type and enforces a re-computation of the current
        /// type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidateType()
        {
            if (!HasStaticType)
                type = null;
        }

        /// <summary>
        /// Computes the current type.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <returns>The resolved type node.</returns>
        protected virtual TypeNode ComputeType(in ValueInitializer initializer) =>
            throw new InvalidOperationException();

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
        protected internal abstract Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder);

        /// <summary>
        /// Verifies that the this value is not sealed.
        /// </summary>
        protected void VerifyNotSealed() =>
            Debug.Assert(!IsSealed, "Value has been sealed");

        /// <summary>
        /// Seals this value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Seal()
        {
            VerifyNotSealed();
            ValueFlags |= ValueFlags.IsSealed;

            // Wire uses
            for (int i = 0, e = values.Count; i < e; ++i)
            {
                Value value = values[i];
                if (value.CanHaveUses)
                    value.AddUse(this, i);
            }

            InferLocation(Nodes);
        }

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="value1">The first child node.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Seal(ValueReference value1)
        {
            values.Reserve(1);

            values.Add(value1);

            Seal();
        }

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="value1">The first child node.</param>
        /// <param name="value2">The second child node.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Seal(ValueReference value1, ValueReference value2)
        {
            values.Reserve(2);

            values.Add(value1);
            values.Add(value2);

            Seal();
        }

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="value1">The first child node.</param>
        /// <param name="value2">The second child node.</param>
        /// <param name="value3">The third child node.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Seal(
            ValueReference value1,
            ValueReference value2,
            ValueReference value3)
        {
            values.Reserve(3);

            values.Add(value1);
            values.Add(value2);
            values.Add(value3);

            Seal();
        }

        /// <summary>
        /// Seals this value.
        /// </summary>
        /// <param name="valueList">The nested child nodes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Seal(ref ValueList valueList)
        {
            VerifyNotSealed();
            valueList.MoveTo(ref values);

            Seal();
        }

        /// <summary>
        /// Replaces this value with the given value.
        /// </summary>
        /// <param name="other">The other value.</param>
        public void Replace(Value other)
        {
            this.AssertNotNull(other);
            this.Assert(CanBeReplaced && !IsReplaced);

            var target = other.Resolve();
            this.Assert(target != this);
            Replacement = target;

            if (target.CanHaveUses)
            {
                // Propagate uses
                foreach (var use in uses)
                    Replacement.AddUse(use.Target, use.Index);
            }

            // Notify nodes
            foreach (var use in uses)
                use.Target.OnReplacedNode();
        }

        /// <summary>
        /// Invoked when an attached node is replaced.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnReplacedNode() => InvalidateType();

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
        /// Returns true if the given value is the same value.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>True, if the given value is the same value.</returns>
        public bool Equals(Value other) => other == this;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all child values.
        /// </summary>
        /// <returns>An enumerator to enumerate all child values.</returns>
        public ReadOnlySpan<ValueReference>.Enumerator GetEnumerator() =>
            Nodes.GetEnumerator();

        #endregion

        #region Object

        /// <summary>
        /// Returns the argument string (operation arguments) of this node.
        /// </summary>
        /// <returns>The argument string.</returns>
        protected virtual string ToArgString() =>
            "(" +
            Nodes.ToString(new ValueReference.ToReferenceFormatter()) +
            ")";

        /// <summary>
        /// Returns the string representation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public sealed override string ToString()
        {
            var argString = ToArgString();
            return string.IsNullOrEmpty(argString)
                ? ToReferenceString()
                : ToReferenceString() + ": " + argString;
        }

        /// <summary>
        /// Returns true if the given object is equal to the current value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current value.</returns>
        public override bool Equals(object obj) => obj == this;

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode() => base.GetHashCode();

        #endregion
    }

    /// <summary>
    /// A parent value container that holds and manages values.
    /// </summary>
    public abstract class ValueParent : Node
    {
        #region Instance

        /// <summary>
        /// Constructs a new value parent.
        /// </summary>
        /// <param name="location">The current location.</param>
        protected ValueParent(Location location)
            : base(location)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this parent container is a block.
        /// </summary>
        public bool IsBasicBlock => this is BasicBlock;

        /// <summary>
        /// Returns true if this container is method.
        /// </summary>
        public bool IsMethod => this is Method;

        #endregion
    }
}
