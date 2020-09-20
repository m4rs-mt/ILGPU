// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a unary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value node,
            UnaryArithmeticKind kind) =>
            CreateArithmetic(
                location,
                node,
                kind,
                ArithmeticFlags.None);

        /// <summary>
        /// Creates a unary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value node,
            UnaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            if (UseConstantPropagation)
            {
                // Check for constants
                if (node is PrimitiveValue value)
                    return UnaryArithmeticFoldConstants(location, value, kind);

                var isUnsigned = (flags & ArithmeticFlags.Unsigned) ==
                    ArithmeticFlags.Unsigned;
                switch (kind)
                {
                    case UnaryArithmeticKind.Not:
                        if (node is UnaryArithmeticValue otherValue &&
                            otherValue.Kind == UnaryArithmeticKind.Not)
                        {
                            return otherValue.Value;
                        }

                        if (node is CompareValue compareValue)
                        {
                            // When the comparison is inverted, and we are comparing
                            // floats, toggle between ordered/unordered float comparison.
                            var compareFlags = compareValue.Flags;
                            if (compareValue.Left.BasicValueType.IsFloat() &&
                                compareValue.Right.BasicValueType.IsFloat())
                            {
                                compareFlags ^= CompareFlags.UnsignedOrUnordered;
                            }

                            return CreateCompare(
                                location,
                                compareValue.Left,
                                compareValue.Right,
                                CompareValue.Invert(compareValue.Kind),
                                compareFlags);
                        }
                        break;
                    case UnaryArithmeticKind.Neg:
                        if (node.BasicValueType == BasicValueType.Int1)
                        {
                            return CreateArithmetic(
                                location,
                                node,
                                UnaryArithmeticKind.Not);
                        }
                        break;
                    case UnaryArithmeticKind.Abs:
                        if (isUnsigned)
                            return node;
                        break;
                }
            }

            return Append(new UnaryArithmeticValue(
                GetInitializer(location),
                node,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a binary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value left,
            Value right,
            BinaryArithmeticKind kind) =>
            CreateArithmetic(
                location,
                left,
                right,
                kind,
                ArithmeticFlags.None);

        /// <summary>
        /// Creates a binary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value left,
            Value right,
            BinaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            // TODO: add additional partial arithmetic simplifications in a generic way
            if (UseConstantPropagation && left is PrimitiveValue leftValue)
            {
                // Check for constants
                if (right is PrimitiveValue rightConstant)
                {
                    return BinaryArithmeticFoldConstants(
                        location,
                        leftValue,
                        rightConstant,
                        kind,
                        flags);
                }

                if (kind == BinaryArithmeticKind.Div)
                {
                    switch (left.BasicValueType)
                    {
                        case BasicValueType.Float32:
                            if (leftValue.Float32Value == 1.0f)
                            {
                                return CreateArithmetic(
                                    location,
                                    right,
                                    UnaryArithmeticKind.RcpF);
                            }
                            break;
                        case BasicValueType.Float64:
                            if (leftValue.Float64Value == 1.0)
                            {
                                return CreateArithmetic(
                                    location,
                                    right,
                                    UnaryArithmeticKind.RcpF);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            // TODO: remove the following hard-coded rules
            if (right is PrimitiveValue rightValue && left.BasicValueType.IsInt())
            {
                if (Utilities.IsPowerOf2(rightValue.RawValue) &&
                    (kind == BinaryArithmeticKind.Div ||
                    kind == BinaryArithmeticKind.Mul))
                {
                    var shiftAmount = CreatePrimitiveValue(
                        location,
                        (int)Math.Log(
                            Math.Abs((double)rightValue.RawValue),
                            2.0));
                    var leftKind = Utilities.Select(
                        kind == BinaryArithmeticKind.Div,
                        BinaryArithmeticKind.Shr,
                        BinaryArithmeticKind.Shl);
                    var rightKind = Utilities.Select(
                        leftKind == BinaryArithmeticKind.Shr,
                        BinaryArithmeticKind.Shl,
                        BinaryArithmeticKind.Shr);
                    return CreateArithmetic(
                        location,
                        left,
                        shiftAmount,
                        Utilities.Select(
                            rightValue.RawValue > 0,
                            leftKind,
                            rightKind));
                }
                else if (
                    rightValue.RawValue == 0 &&
                    (kind == BinaryArithmeticKind.Add ||
                    kind == BinaryArithmeticKind.Sub))
                {
                    return left;
                }
            }

            switch (kind)
            {
                case BinaryArithmeticKind.And:
                case BinaryArithmeticKind.Or:
                case BinaryArithmeticKind.Xor:
                    if (left.BasicValueType.IsFloat())
                    {
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedArithmeticArgumentType,
                            left.BasicValueType);
                    }

                    break;

                case BinaryArithmeticKind.Atan2F:
                case BinaryArithmeticKind.PowF:
                    if (!left.BasicValueType.IsFloat())
                    {
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedArithmeticArgumentType,
                            left.BasicValueType);
                    }

                    break;
            }

            return Append(new BinaryArithmeticValue(
                GetInitializer(location),
                left,
                right,
                kind,
                flags));
        }

        /// <summary>
        /// Creates a ternary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The second operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value first,
            Value second,
            Value third,
            TernaryArithmeticKind kind) =>
            CreateArithmetic(
                location,
                first,
                second,
                third,
                kind,
                ArithmeticFlags.None);

        /// <summary>
        /// Creates a ternary arithmetic operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The second operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the arithmetic operation.</returns>
        public ValueReference CreateArithmetic(
            Location location,
            Value first,
            Value second,
            Value third,
            TernaryArithmeticKind kind,
            ArithmeticFlags flags)
        {
            if (UseConstantPropagation)
            {
                // Check for constants
                if (first is PrimitiveValue firstValue &&
                    second is PrimitiveValue secondValue)
                {
                    var value = BinaryArithmeticFoldConstants(
                        location,
                        firstValue,
                        secondValue,
                        TernaryArithmeticValue.GetLeftBinaryKind(kind),
                        flags);

                    // Try to fold right hand side as well
                    var rightOperation = TernaryArithmeticValue.GetRightBinaryKind(kind);
                    return CreateArithmetic(
                        location,
                        value,
                        third,
                        rightOperation);
                }
            }

            return Append(new TernaryArithmeticValue(
                GetInitializer(location),
                first,
                second,
                third,
                kind,
                flags));
        }
    }
}
