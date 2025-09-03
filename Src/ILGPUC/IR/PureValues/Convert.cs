// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Flags of a convert operation.
/// </summary>
[Flags]
enum ConvertFlags
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
static class ConvertFlagsExtensions
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
sealed partial class ConvertValue : PureValue
{
    /// <summary>
    /// Constructs a new convert value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="targetType">The target type to convert the value to.</param>
    /// <param name="flags">The operation flags.</param>
    public ConvertValue(
        in PureValueInitializer initializer,
        Value value,
        TypeValue targetType,
        ConvertFlags flags)
        : base(initializer, targetType)
    {
        Flags = flags;

        Seal(value);
    }

    /// <summary>
    /// Returns the operand.
    /// </summary>
    public Value Value => GetValue<Value>(0);

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

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateConvert(
            Location,
            rewriter.Rewrite(Value),
            rewriter.RewriteAs<TypeValue>(Type),
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "conv";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Value.ToReferenceString()} -> {TargetType} [{Flags}]";
}
