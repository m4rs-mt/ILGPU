// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents flags of an atomic operation.
    /// </summary>
    [Flags]
    public enum AtomicFlags
    {
        /// <summary>
        /// No special flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// The operation has unsigned semantics.
        /// </summary>
        Unsigned = 1,
    }

    /// <summary>
    /// Represents a general atomic value.
    /// </summary>
    public abstract class AtomicValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract atomic value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicValue(in ValueInitializer initializer, AtomicFlags flags)
            : base(initializer)

        {
            Flags = flags;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target view.
        /// </summary>
        public ValueReference Target => this[0];

        /// <summary>
        /// Returns the target address space this atomic operates on.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace =>
            Target.Type.As<AddressSpaceType>(this).AddressSpace;

        /// <summary>
        /// Returns the target value.
        /// </summary>
        public ValueReference Value => this[1];

        /// <summary>
        /// Returns the operation flags.
        /// </summary>
        public AtomicFlags Flags { get; }

        /// <summary>
        /// Returns the associated arithmetic basic value type.
        /// </summary>
        public ArithmeticBasicValueType ArithmeticBasicValueType =>
            BasicValueType.GetArithmeticBasicValueType(IsUnsigned);

        /// <summary>
        /// Returns true if the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsUnsigned => (Flags & AtomicFlags.Unsigned) ==
            AtomicFlags.Unsigned;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Value.Type;

        #endregion
    }

    /// <summary>
    /// Represents the kind of an atomic operation.
    /// </summary>
    public enum AtomicKind
    {
        /// <summary>
        /// An XCHG operation.
        /// </summary>
        Exchange,

        /// <summary>
        /// An add operation.
        /// </summary>
        Add,

        /// <summary>
        /// An and operation.
        /// </summary>
        And,

        /// <summary>
        /// An or operation.
        /// </summary>
        Or,

        /// <summary>
        /// An xor operation.
        /// </summary>
        Xor,

        /// <summary>
        /// A max operation.
        /// </summary>
        Max,

        /// <summary>
        /// A min operation.
        /// </summary>
        Min,
    }

    /// <summary>
    /// Represents a generic atomic operation.
    /// </summary>
    [ValueKind(ValueKind.GenericAtomic)]
    public sealed class GenericAtomic : AtomicValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic atomic operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal GenericAtomic(
            in ValueInitializer initializer,
            ValueReference target,
            ValueReference value,
            AtomicKind kind,
            AtomicFlags flags)
            : base(initializer, flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);

            Kind = kind;
            Seal(target, value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GenericAtomic;

        /// <summary>
        /// The operation kind.
        /// </summary>
        public AtomicKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAtomic(
                Location,
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value),
                Kind,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "atomic" + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Target}, {Value} [{Flags}]";

        #endregion
    }

    /// <summary>
    /// Represents an atomic compare-and-swap operation.
    /// </summary>
    [ValueKind(ValueKind.AtomicCAS)]
    public sealed class AtomicCAS : AtomicValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new atomic compare-and-swap operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="compareValue">The comparison value.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicCAS(
            in ValueInitializer initializer,
            ValueReference target,
            ValueReference value,
            ValueReference compareValue,
            AtomicFlags flags)
            : base(initializer, flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);
            Debug.Assert(value.Type == compareValue.Type);

            Seal(target, value, compareValue);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AtomicCAS;

        /// <summary>
        /// Returns the comparison value.
        /// </summary>
        public ValueReference CompareValue => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAtomicCAS(
                Location,
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value),
                rebuilder.Rebuild(CompareValue),
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "atomicCAS";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Target}, {Value}, {CompareValue}";

        #endregion
    }
}
