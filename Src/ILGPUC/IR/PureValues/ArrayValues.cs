// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues.Construction;
using System;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an allocation operation of a new array in a particular address space.
/// </summary>
sealed partial class ArrayValue : PureValue
{
    #region Nested Types

    /// <summary>
    /// An instance builder for array values.
    /// </summary>
    /// <param name="pureBuilder">The current IR builder.</param>
    /// <param name="location">The current location.</param>
    /// <param name="arrayType">The parent array type of this value.</param>
    internal struct Builder(
        PureValueBuilder pureBuilder,
        Location location,
        ArrayType arrayType)
    {
        private ValueBuilderList _builder = ValueBuilderList.Create(
            pureBuilder.Generation,
            arrayType.NumDimensions);

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public ArrayType ArrayType { get; } = arrayType;

        /// <summary>
        /// The number of dimensions.
        /// </summary>
        public readonly int Count => _builder.Count;

        /// <summary>
        /// Adds the given dimension length to the array value builder.
        /// </summary>
        /// <param name="dimension">The value to add.</param>
        public void Add(Value dimension)
        {
            location.Assert(Count < ArrayType.NumDimensions);

            _builder.Add(dimension);
        }

        /// <summary>
        /// Constructs a new value that represents the current array value.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public ArrayValue Seal() =>
            pureBuilder.FinishArrayValue(location, ArrayType, ref _builder);
    }

    #endregion

    #region ArrayValue Instance

    /// <summary>
    /// Constructs an array with back buffer and dimension lengths.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="arrayType">The parent array type of this array.</param>
    /// <param name="values">
    /// The combined value list: [backBuffer, dim0, dim1, ..., dimN-1].
    /// </param>
    public ArrayValue(
        in PureValueInitializer initializer,
        ArrayType arrayType,
        ref ValueBuilderList values)
        : base(initializer, arrayType)
    {
        Seal(ref values);
    }

    /// <summary>
    /// Returns the array type of this value.
    /// </summary>
    public new ArrayType Type => GetTypeAs<ArrayType>();

    /// <summary>
    /// Returns the array's element type.
    /// </summary>
    public TypeValue ElementType => Type.ElementType;

    /// <summary>
    /// Returns the number of array dimensions.
    /// </summary>
    public int NumDimensions => Type.NumDimensions;

    /// <summary>
    /// Returns the underlying storage block.
    /// </summary>
    public ModuleValue BackBuffer => GetValue<ModuleValue>(0);

    /// <summary>
    /// Returns all stored dimension length values.
    /// </summary>
    public ReadOnlySpan<Value> Dimensions => Values[1..];

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var newBackBuffer = rewriter.RewriteAs<ModuleValue>(BackBuffer);
        if (newBackBuffer is null) return null;

        var newArrayType = rewriter.RewriteAs<ArrayType>(Type);
        var combined = ValueBuilderList.Create(
            rewriter.Builder.Generation,
            1 + NumDimensions);
        combined.Add(newBackBuffer);
        foreach (var dim in Dimensions)
            combined.Add(rewriter.Rewrite(dim));
        return rewriter.Builder.FinishArrayValueCombined(
            Location,
            newArrayType,
            ref combined);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        $"array{NumDimensions}D -> {BackBuffer.ToReferenceString}";

    #endregion
}

/// <summary>
/// Represents an abstract array value operation.
/// </summary>
interface IArrayValueOperation
{
    /// <summary>
    /// Returns the source array value.
    /// </summary>
    Value ArrayValue { get; }
}

/// <summary>
/// Gets the length of an array value or a particular array dimension.
/// </summary>
sealed partial class GetArrayLength : PureValue, IArrayValueOperation
{
    /// <summary>
    /// Constructs a array.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="arrayValue">The parent array value.</param>
    /// <param name="dimension">The value of the dimension to get.</param>
    public GetArrayLength(
        in PureValueInitializer initializer,
        Value arrayValue,
        Value dimension)
        : base(
            initializer,
            initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
    {
        Seal(arrayValue, dimension);
    }

    /// <summary>
    /// Returns the source array value.
    /// </summary>
    public Value ArrayValue => GetValue<Value>(0);

    /// <summary>
    /// Returns the source dimension.
    /// </summary>
    public Value Dimension => GetValue<Value>(1);

    /// <summary>
    /// Returns true if this length value returns the full linear length of the array.
    /// </summary>
    public bool IsFullLength => Dimension is UndefinedValue;

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGetArrayLength(
            Location,
            rewriter.Rewrite(ArrayValue),
            rewriter.Rewrite(Dimension));

    /// <inheritdoc/>
    protected override string ToPrefixString() =>
        IsFullLength ? "array.dim" : "array.len";

    /// <inheritdoc/>
    protected override string ToArgString() =>
        IsFullLength
        ? Dimension.ToString()
        : string.Empty;
}
