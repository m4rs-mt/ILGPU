// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;

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
        /// The convert operation treats the input value as unsigned.
        /// </summary>
        SourceUnsigned = 1,

        /// <summary>
        /// The convert operation results in an unsigned value.
        /// </summary>
        TargetUnsigned = 2,
    }

    /// <summary>
    /// Internal conversion flags extensions.
    /// </summary>
    internal static class ConvertFlagsExtensions
    {
        /// <summary>
        /// Converts the given flags into source unsigned flags.
        /// </summary>
        /// <param name="flags">The flags to convert.</param>
        /// <returns>The converted flags.</returns>
        internal static ConvertFlags ToSourceUnsignedFlags(this ConvertFlags flags) =>
            (flags & ConvertFlags.TargetUnsigned) != ConvertFlags.TargetUnsigned
            ? flags
            : (flags & ~ConvertFlags.TargetUnsigned) | ConvertFlags.SourceUnsigned;
    }

    /// <summary>
    /// Converts a node into a target type.
    /// </summary>
    [ValueKind(ValueKind.Convert)]
    public sealed class ConvertValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        /// <param name="flags">The operation flags.</param>
        internal ConvertValue(
            in ValueInitializer initializer,
            ValueReference value,
            TypeNode targetType,
            ConvertFlags flags)
            : base(initializer, targetType)
        {
            Flags = flags;

            Seal(value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Convert;

        /// <summary>
        /// Returns the operand.
        /// </summary>
        public ValueReference Value => this[0];

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
        /// Returns true if the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsSourceUnsigned => (Flags & ConvertFlags.SourceUnsigned) ==
            ConvertFlags.SourceUnsigned;

        /// <summary>
        /// Returns true if the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsResultUnsigned => (Flags & ConvertFlags.TargetUnsigned) ==
            ConvertFlags.TargetUnsigned;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateConvert(
                Location,
                rebuilder.Rebuild(Value),
                Type,
                Flags);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer)
        {
            writer.Write(TargetType);
            writer.Write(Flags);
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "conv";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Value} -> {TargetType} [{Flags}]";

        #endregion
    }
}
