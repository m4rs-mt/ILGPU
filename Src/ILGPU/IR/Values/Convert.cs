// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Flags of a convert operation.
    /// </summary>
    [Flags]
    public enum ConvertFlags
    {
        /// <summary>
        /// No flags (default).
        /// </summary>
        None,

        /// <summary>
        /// The convert operation has overflow semantics.
        /// </summary>
        Overflow = 1,

        /// <summary>
        /// The convert operation treats the input value as unsigned.
        /// </summary>
        SourceUnsigned = 2,

        /// <summary>
        /// The convert operation has overflow semantics and the
        /// overflow check is based on unsigned semantics.
        /// </summary>
        OverflowSourceUnsigned = 3,

        /// <summary>
        /// The convert operation results in an unsigned value.
        /// </summary>
        TargetUnsigned = 4,
    }

    internal static class ConvertFlagsExtensions
    {
        internal static ConvertFlags ToSourceUnsignedFlags(this ConvertFlags flags)
        {
            if ((flags & ConvertFlags.TargetUnsigned) != ConvertFlags.TargetUnsigned)
                return flags;
            return (flags & ~ConvertFlags.TargetUnsigned) | ConvertFlags.SourceUnsigned;
        }
    }

    /// <summary>
    /// Converts a node into a target type.
    /// </summary>
    public sealed class ConvertValue : Value
    {
        #region Static

        /// <summary>
        /// Computes a convert node type.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(TypeNode targetType) =>
            targetType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        /// <param name="flags">The operation flags.</param>
        internal ConvertValue(
            BasicBlock basicBlock,
            ValueReference value,
            TypeNode targetType,
            ConvertFlags flags)
            : base(ValueKind.Convert, basicBlock, ComputeType(targetType))
        {
            ConvertType = targetType;
            Flags = flags;

            Seal(ImmutableArray.Create(value));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the operand.
        /// </summary>
        public ValueReference Value => this[0];

        /// <summary>
        /// Returns the target type.
        /// </summary>
        public TypeNode ConvertType { get; }

        /// <summary>
        /// Returns the associated flags.
        /// </summary>
        public ConvertFlags Flags { get; }

        /// <summary>
        /// Returns the source type to convert the value from.
        /// </summary>
        public ArithmeticBasicValueType SourceType =>
            Value.BasicValueType.GetArithmeticBasicValueType(IsSourceUnsigned);

        /// <summary>
        /// Returns the target type to convert the value to.
        /// </summary>
        public ArithmeticBasicValueType TargetType =>
            BasicValueType.GetArithmeticBasicValueType(IsResultUnsigned);

        /// <summary>
        /// Returns true iff the operation has enabled overflow semantics.
        /// </summary>
        public bool CanOverflow => (Flags & ConvertFlags.Overflow) ==
            ConvertFlags.Overflow;

        /// <summary>
        /// Returns true iff the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsSourceUnsigned => (Flags & ConvertFlags.SourceUnsigned) ==
            ConvertFlags.SourceUnsigned;

        /// <summary>
        /// Returns true iff the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsResultUnsigned => (Flags & ConvertFlags.TargetUnsigned) ==
            ConvertFlags.TargetUnsigned;

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ConvertType);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateConvert(
                rebuilder.Rebuild(Value),
                ConvertType,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "conv";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Value} -> {TargetType.ToString()} [{Flags}]";

        #endregion
    }
}
