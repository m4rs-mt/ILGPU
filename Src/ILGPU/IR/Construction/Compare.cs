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
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the compare operation.</returns>
        public ValueReference CreateCompare(
            Value left,
            Value right,
            CompareKind kind) =>
            CreateCompare(left, right, kind, CompareFlags.None);

        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the compare operation.</returns>
        public ValueReference CreateCompare(
            Value left,
            Value right,
            CompareKind kind,
            CompareFlags flags)
        {
            Debug.Assert(left != null, "Invalid left node");
            Debug.Assert(right != null, "Invalid right node");

            if (UseConstantPropagation)
            {
                var leftValue = left as PrimitiveValue;
                var rightValue = right as PrimitiveValue;
                if (leftValue != null && rightValue != null)
                    return CompareFoldConstants(leftValue, rightValue, kind, flags);

                if (leftValue != null)
                {
                    return CreateCompare(
                        right,
                        left,
                        CompareValue.InvertIfNonCommutative(kind),
                        flags);
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
                                : CreateArithmetic(left, UnaryArithmeticKind.Not)
                            : rightValue.Int1Value
                                ? CreateArithmetic(left, UnaryArithmeticKind.Not)
                                : (ValueReference)left;
                    }
                }
            }

            return Append(new CompareValue(
                Context,
                BasicBlock,
                left,
                right,
                kind,
                flags));
        }

    }
}
