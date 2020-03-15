// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        #region Static

        /// <summary>
        /// Computes an atomic node type.
        /// </summary>
        /// <param name="value">The atomic value operand.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(ValueReference value) =>
            value.Type;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new abstract atomic value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="arguments">Additional arguments.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicValue(
            BasicBlock basicBlock,
            ValueReference target,
            ValueReference value,
            ImmutableArray<ValueReference> arguments,
            AtomicFlags flags)
            : base(
                  basicBlock,
                  ImmutableArray.Create(target, value).AddRange(arguments),
                  ComputeType(value))

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
        /// Returns true iff the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsUnsigned => (Flags & AtomicFlags.Unsigned) ==
            AtomicFlags.Unsigned;

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected sealed override TypeNode UpdateType(IRContext context) =>
            ComputeType(Value);

        #endregion
    }

    /// <summary>
    /// Represents the kind of an atomic operation.
    /// </summary>
    public enum AtomicKind
    {
        /// <summary>
        /// An xchg operation.
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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal GenericAtomic(
            BasicBlock basicBlock,
            ValueReference target,
            ValueReference value,
            AtomicKind kind,
            AtomicFlags flags)
            : base(
                  basicBlock,
                  target,
                  value,
                  ImmutableArray<ValueReference>.Empty,
                  flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);

            Kind = kind;
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
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAtomic(
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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="compareValue">The comparison value.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicCAS(
            BasicBlock basicBlock,
            ValueReference target,
            ValueReference value,
            ValueReference compareValue,
            AtomicFlags flags)
            : base(
                  basicBlock,
                  target,
                  value,
                  ImmutableArray.Create(compareValue),
                  flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);
            Debug.Assert(value.Type == compareValue.Type);
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
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAtomicCAS(
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
