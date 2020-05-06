// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the compare operation.</returns>
        public ValueReference CreateCompare(
            Location location,
            Value left,
            Value right,
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
        public ValueReference CreateCompare(
            Location location,
            Value left,
            Value right,
            CompareKind kind,
            CompareFlags flags)
        {
            if (UseConstantPropagation)
            {
                var leftValue = left as PrimitiveValue;
                var rightValue = right as PrimitiveValue;
                if (leftValue != null && rightValue != null)
                {
                    return CompareFoldConstants(
                        location,
                        leftValue,
                        rightValue,
                        kind,
                        flags);
                }

                if (leftValue != null)
                {
                    // If the comparison was inverted, and we are comparing floats,
                    // toggle between ordered/unordered float comparison.
                    var compareKind = CompareValue.InvertIfNonCommutative(kind);
                    var compareFlags = flags;
                    if (compareKind != kind &&
                        left.BasicValueType.IsFloat() &&
                        right.BasicValueType.IsFloat())
                    {
                        compareFlags ^= CompareFlags.UnsignedOrUnordered;
                    }

                    return CreateCompare(
                        location,
                        right,
                        left,
                        compareKind,
                        compareFlags);
                }

                if (left.Type is PrimitiveType leftType &&
                    leftType.BasicValueType == BasicValueType.Int1)
                {
                    // Bool comparison -> convert to logical operation
                    if (rightValue != null)
                    {
                        return kind == CompareKind.Equal
                            ? rightValue.Int1Value
                                ? (ValueReference)left
                                : CreateArithmetic(
                                    location,
                                    left,
                                    UnaryArithmeticKind.Not)
                            : rightValue.Int1Value
                                ? CreateArithmetic(
                                    location,
                                    left,
                                    UnaryArithmeticKind.Not)
                                : (ValueReference)left;
                    }
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
}
