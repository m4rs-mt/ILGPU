// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerThreadIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Rewriting;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers internal high-level thread intrinsics.
/// </summary>
sealed class LowerThreadIntrinsics : UnorderedTransformation
{
    #region Lowering

    /// <summary>
    /// Lowers the given thread value.
    /// </summary>
    /// <typeparam name="TValue">The thread value type.</typeparam>
    /// <param name="builder">The current builder.</param>
    /// <param name="source">The source value.</param>
    /// <param name="newVariable">The new variable.</param>
    /// <returns>The created value.</returns>
    internal delegate ValueReference LoweringHandler<TValue>(
        BasicBlock.Builder builder,
        TValue source,
        Value newVariable);

    /// <summary>
    /// Lowers a broadcast value by constructing a new one.
    /// </summary>
    public static ValueReference LowerBroadcast(
        BasicBlock.Builder builder,
        Broadcast source,
        Value newVariable) =>
        builder.CreateBroadcast(
            source.Location,
            newVariable,
            source.Origin,
            source.Kind);

    /// <summary>
    /// Lowers a warp shuffle value by constructing a new one.
    /// </summary>
    public static ValueReference LowerShuffle(
        BasicBlock.Builder builder,
        WarpShuffle source,
        Value newVariable) =>
        builder.CreateShuffle(
            source.Location,
            newVariable,
            source.Origin,
            source.Kind);

    /// <summary>
    /// Lowers a primitive type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="context">The current rewriter context.</param>
    /// <param name="sourceValue">The source value to get the values from.</param>
    /// <param name="variable">The source variable.</param>
    /// <param name="handler">The lowering handler.</param>
    /// <returns>The lowered thread value.</returns>
    private static Value LowerPrimitive<TValue>(
        RewriterContext context,
        TValue sourceValue,
        Value variable,
        LoweringHandler<TValue> handler)
        where TValue : ThreadValue
    {
        var builder = context.Builder;
        var primitiveType = variable.Type.AsNotNullCast<PrimitiveType>();
        Value value = variable;
        if (primitiveType.BasicValueType < BasicValueType.Int32)
        {
            value = builder.CreateConvert(
                sourceValue.Location,
                value,
                builder.GetPrimitiveType(BasicValueType.Int32));
        }

        var result = handler(builder, sourceValue, value);
        if (primitiveType.BasicValueType < BasicValueType.Int32)
        {
            result = builder.CreateConvert(
                sourceValue.Location,
                result,
                variable.Type);
        }
        return result;
    }

    /// <summary>
    /// Lowers a type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="context">The current rewriter context.</param>
    /// <param name="value">The source value to get the values from.</param>
    /// <param name="handler">The lowering handler.</param>
    private static void Lower<TValue>(
        in RewriterContext context,
        TValue value,
        LoweringHandler<TValue> handler)
        where TValue : ThreadValue
    {
        var newValue = context.LowerValue(
            value,
            value.Variable,
            (context, sourceValue, variable) =>
                LowerPrimitive(context, sourceValue, variable, handler));
        context.ReplaceAndRemove(value, newValue);
    }

    #endregion

    #region Rewriter

    /// <summary>
    /// The internal rewriter.
    /// </summary>
    private static readonly Rewriter Rewriter = new();

    /// <summary>
    /// Registers all rewriting patterns.
    /// </summary>
    static LowerThreadIntrinsics()
    {
        Rewriter.Add<Broadcast>(
            broadcast => !broadcast.IsBuiltIn,
            (context, value) => Lower(context, value, LowerBroadcast));
        Rewriter.Add<WarpShuffle>(
            shuffle => !shuffle.IsBuiltIn,
            (context, value) => Lower(context, value, LowerShuffle));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies the lowering of thread intrinsics transformation.
    /// </summary>
    protected override void PerformTransformation(
        IRContext context,
        Method.Builder builder) =>
        Rewriter.Rewrite(builder.SourceBlocks, builder);

    #endregion
}
