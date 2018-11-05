// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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
        /// <summary>
        /// Constructs a new abstract atomic value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory operation.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="arguments">Additional arguments.</param>
        /// <param name="type">The operation type.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicValue(
            ValueGeneration generation,
            ValueReference parent,
            ValueReference target,
            ValueReference value,
            ImmutableArray<ValueReference> arguments,
            TypeNode type,
            AtomicFlags flags)
            : base(
                  generation,
                  parent,
                  ImmutableArray.Create(target, value).AddRange(arguments),
                  type)
        {
            Flags = flags;
        }

        /// <summary>
        /// Returns the target view.
        /// </summary>
        public ValueReference Target => this[1];

        /// <summary>
        /// Returns the target value.
        /// </summary>
        public ValueReference Value => this[2];

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
    public sealed class GenericAtomic : AtomicValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic atomic operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory operation.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal GenericAtomic(
            ValueGeneration generation,
            ValueReference parent,
            ValueReference target,
            ValueReference value,
            AtomicKind kind,
            AtomicFlags flags)
            : base(
                  generation,
                  parent,
                  target,
                  value,
                  ImmutableArray<ValueReference>.Empty,
                  value.Type,
                  flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);

            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The operation kind.
        /// </summary>
        public AtomicKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAtomic(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value),
                Kind,
                Flags);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

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
    public sealed class AtomicCAS : AtomicValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new atomic compare-and-swap operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory operation.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="compareValue">The comparison value.</param>
        /// <param name="flags">The operation flags.</param>
        internal AtomicCAS(
            ValueGeneration generation,
            ValueReference parent,
            ValueReference target,
            ValueReference value,
            ValueReference compareValue,
            AtomicFlags flags)
            : base(
                  generation,
                  parent,
                  target,
                  value,
                  ImmutableArray.Create(compareValue),
                  value.Type,
                  flags)
        {
            Debug.Assert(value.Type == (target.Type as PointerType).ElementType);
            Debug.Assert(value.Type == compareValue.Type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the comparison value.
        /// </summary>
        public ValueReference CompareValue => this[3];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAtomicCAS(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value),
                rebuilder.Rebuild(CompareValue),
                Flags);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "atomicCAS";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Target}, {Value}, {CompareValue}";

        #endregion
    }
}
