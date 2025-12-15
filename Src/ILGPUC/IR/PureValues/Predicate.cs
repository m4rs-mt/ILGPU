// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Predicate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents a conditional predicate.
/// </summary>
sealed partial class Predicate : PureValue
{
    /// <summary>
    /// Constructs a new predicate.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="trueValue">The true value.</param>
    /// <param name="falseValue">The false value.</param>
    public Predicate(
        in PureValueInitializer initializer,
        Value condition,
        Value trueValue,
        Value falseValue)
        : base(initializer, trueValue.Type)
    {
        Location.Assert(
            condition.Type is PrimitiveType &&
            condition.Type.BasicValueType == BasicValueType.Int1);
        Seal(condition, trueValue, falseValue);
    }

    /// <summary>
    /// Returns the associated predicate value.
    /// </summary>
    public Value Condition => GetValue<Value>(0);

    /// <summary>
    /// Returns the true value.
    /// </summary>
    public Value TrueValue => GetValue<Value>(1);

    /// <summary>
    /// Returns the false value.
    /// </summary>
    public Value FalseValue => GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreatePredicate(
            Location,
            rewriter.Rewrite(Condition),
            rewriter.Rewrite(TrueValue),
            rewriter.Rewrite(FalseValue));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "pred";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Condition} ? {TrueValue} : {FalseValue}";
}
