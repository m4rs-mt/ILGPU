// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Creates a compare operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>A node that represents the compare operation.</returns>
    public Value? CreateCompare(
        Location location,
        Value? left,
        Value? right,
        CompareKind kind) =>
        CreateCompare(
            location,
            left,
            right,
            kind,
            CompareFlags.None);

    /// <summary>
    /// Creates a compare operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">Operation flags.</param>
    /// <returns>A node that represents the compare operation.</returns>
    public Value? CreateCompare(
        Location location,
        Value? left,
        Value? right,
        CompareKind kind,
        CompareFlags flags)
    {
        if (left is null || right is null) return null;

        var leftValue = left as PrimitiveValue;
        var rightValue = right as PrimitiveValue;
        if (leftValue is not null && rightValue is not null)
        {
            return CompareFoldConstants(
                location,
                leftValue,
                rightValue,
                kind,
                flags);
        }

        // Check whether we should move constants to the RHS
        if (leftValue is not null)
        {
            // Adjust the compare kind and flags for swapping both operands
            kind = CompareValue.SwapOperands(
                kind,
                left.BasicValueType,
                right.BasicValueType,
                ref flags);

            // Create a new compare value using the updated kind and flags
            return CreateCompare(
                location,
                right,
                left,
                kind,
                flags);
        }

        if (left.Type is PrimitiveType leftType &&
            leftType.BasicValueType == BasicValueType.Int1)
        {
            // Bool comparison -> convert to logical operation
            if (rightValue is not null)
            {
                return kind == CompareKind.Equal
                    ? rightValue.Int1Value
                        ? left
                        : CreateArithmetic(
                            location,
                            left,
                            UnaryArithmeticKind.Not)
                    : rightValue.Int1Value
                        ? CreateArithmetic(
                            location,
                            left,
                            UnaryArithmeticKind.Not)
                        : left;
            }
        }

        return Append(new CompareValue(
            GetInitializer(location),
            left,
            right,
            kind,
            flags));
    }
}
