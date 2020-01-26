// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a unary arithmetic operation.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value node,
            UnaryArithmeticKind kind) =>
            CreateArithmetic(node, kind, ArithmeticFlags.None);

        /// <summary>
        /// Creates a unary arithmetic operation.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value node,
            UnaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            Debug.Assert(node != null, "Invalid node");

            if (UseConstantPropagation)
            {
                // Check for constants
                if (node is PrimitiveValue value)
                    return UnaryArithmeticFoldConstants(value, kind);

                var isUnsigned = (flags & ArithmeticFlags.Unsigned) == ArithmeticFlags.Unsigned;
                switch (kind)
                {
                    case UnaryArithmeticKind.Not:
                        if (node is UnaryArithmeticValue otherValue &&
                            otherValue.Kind == UnaryArithmeticKind.Not)
                            return otherValue.Value;
                        if (node is CompareValue compareValue)
                        {
                            return CreateCompare(
                                compareValue.Left,
                                compareValue.Right,
                                CompareValue.Invert(compareValue.Kind),
                                compareValue.Flags);
                        }
                        break;
                    case UnaryArithmeticKind.Neg:
                        if (node.BasicValueType == BasicValueType.Int1)
                            return CreateArithmetic(node, UnaryArithmeticKind.Not);
                        break;
                    case UnaryArithmeticKind.Abs:
                        if (isUnsigned)
                            return node;
                        break;
                }
            }

            return Append(new UnaryArithmeticValue(
                Context,
                BasicBlock,
                node,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a binary arithmetic operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value left,
            Value right,
            BinaryArithmeticKind kind) =>
            CreateArithmetic(left, right, kind, ArithmeticFlags.None);

        /// <summary>
        /// Creates a binary arithmetic operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value left,
            Value right,
            BinaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            Debug.Assert(left != null, "Invalid left node");
            Debug.Assert(right != null, "Invalid right node");

            if (UseConstantPropagation && left is PrimitiveValue leftValue)
            {
                // Check for constants
                if (right is PrimitiveValue rightValue)
                {
                    return BinaryArithmeticFoldConstants(
                        leftValue, rightValue, kind, flags);
                }

                if (kind == BinaryArithmeticKind.Div)
                {
                    switch (left.BasicValueType)
                    {
                        case BasicValueType.Float32:
                            if (leftValue.Float32Value == 1.0f)
                                return CreateArithmetic(right, UnaryArithmeticKind.RcpF);
                            break;
                        case BasicValueType.Float64:
                            if (leftValue.Float64Value == 1.0)
                                return CreateArithmetic(right, UnaryArithmeticKind.RcpF);
                            break;
                        default:
                            break;
                    }
                }
            }

            switch (kind)
            {
                case BinaryArithmeticKind.And:
                case BinaryArithmeticKind.Or:
                case BinaryArithmeticKind.Xor:
                    if (left.BasicValueType.IsFloat())
                        throw new NotSupportedException(string.Format(
                            ErrorMessages.NotSupportedArithmeticArgumentType,
                            left.BasicValueType));
                    break;

                case BinaryArithmeticKind.Atan2F:
                case BinaryArithmeticKind.PowF:
                    if (!left.BasicValueType.IsFloat())
                        throw new NotSupportedException(string.Format(
                            ErrorMessages.NotSupportedArithmeticArgumentType,
                            left.BasicValueType));
                    break;
            }

            return Append(new BinaryArithmeticValue(
                BasicBlock,
                left,
                right,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a ternary arithmetic operation.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The second operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value first,
            Value second,
            Value third,
            TernaryArithmeticKind kind) =>
            CreateArithmetic(first, second, third, kind, ArithmeticFlags.None);

        /// <summary>
        /// Creates a ternary arithmetic operation.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The second operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Value first,
            Value second,
            Value third,
            TernaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            Debug.Assert(first != null, "Invalid first node");
            Debug.Assert(second != null, "Invalid second node");
            Debug.Assert(third != null, "Invalid third node");

            if (UseConstantPropagation)
            {
                // Check for constants
                if (first is PrimitiveValue firstValue &&
                    second is PrimitiveValue secondValue)
                {
                    var value = BinaryArithmeticFoldConstants(
                        firstValue,
                        secondValue,
                        TernaryArithmeticValue.GetLeftBinaryKind(kind),
                        flags);

                    // Try to fold right hand side as well
                    var rightOperation = TernaryArithmeticValue.GetRightBinaryKind(kind);
                    return CreateArithmetic(value, third, rightOperation);
                }
            }

            return Append(new TernaryArithmeticValue(
                BasicBlock,
                first,
                second,
                third,
                kind,
                flags));
        }
    }
}
