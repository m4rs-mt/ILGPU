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
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

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
            // Check for constants
            if (UseConstantPropagation && node is PrimitiveValue value)
                return UnaryArithmeticFoldConstants(location, value, kind);

            return
                UnaryArithmeticSimplify(
                    location,
                    node,
                    kind,
                    flags)
                ?? Append(new UnaryArithmeticValue(
                    GetInitializer(location),
                    node,
                    kind,
                    flags));
        }

        /// <summary>
        /// Inverts a binary arithmetic value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="binary">The binary operation to invert.</param>
        /// <returns>The inverted binary operation.</returns>
        private ValueReference InvertBinaryArithmetic(
            Location location,
            BinaryArithmeticValue binary) =>
            CreateArithmetic(
                binary.Location,
                CreateArithmetic(
                    location,
                    binary.Left,
                    UnaryArithmeticKind.Not),
                CreateArithmetic(
                    location,
                    binary.Right,
                    UnaryArithmeticKind.Not),
                BinaryArithmeticValue.InvertLogical(binary.Kind),
                binary.Flags);

        /// <summary>
        /// Inverts a compare value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="compareValue">The compare operation to invert.</param>
        /// <returns>The inverted compare value.</returns>
        private ValueReference InvertCompareValue(
            Location location,
            CompareValue compareValue)
        {
            // When the comparison is inverted, and we are comparing
            // floats, toggle between ordered/unordered float
            // comparison
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
            VerifyBinaryArithmeticOperands(location, left, right, kind);

            Value simplified;
            if (UseConstantPropagation && right is PrimitiveValue rightValue)
            {
                // Check for constants
                if (left is PrimitiveValue leftPrimitive)
                {
                    return BinaryArithmeticFoldConstants(
                        location,
                        leftPrimitive,
                        rightValue,
                        kind,
                        flags);
                }

                // Check for simplifications of the RHS
                if ((simplified = BinaryArithmeticSimplify_RHS(
                    location,
                    left,
                    rightValue,
                    kind,
                    flags)) != null)
                {
                    return simplified;
                }

                if (left is BinaryArithmeticValue leftBinary &&
                    leftBinary.Kind == kind &&
                    leftBinary.Right.Resolve() is PrimitiveValue nestedRightValue &&
                    (simplified = BinaryArithmeticSimplify_RHS(
                    location,
                    leftBinary,
                    nestedRightValue,
                    rightValue,
                    kind,
                    flags)) != null)
                {
                    return simplified;
                }
            }

            if (left is PrimitiveValue leftValue)
            {
                // Move constants to the right
                if (kind.IsCommutative())
                {
                    return CreateArithmetic(
                        location,
                        right,
                        left,
                        kind,
                        flags);
                }

                // Check for simplifications of the LHS
                if ((simplified = BinaryArithmeticSimplify_LHS(
                    location,
                    leftValue,
                    right,
                    kind,
                    flags)) != null)
                {
                    return simplified;
                }

                if (right is BinaryArithmeticValue rightBinary &&
                    rightBinary.Kind == kind &&
                    rightBinary.Left.Resolve() is PrimitiveValue nestedLeftValue &&
                    (simplified = BinaryArithmeticSimplify_LHS(
                    location,
                    rightBinary,
                    nestedLeftValue,
                    leftValue,
                    kind,
                    flags)) != null)
                {
                    return simplified;
                }
            }

            return Append(new BinaryArithmeticValue(
                GetInitializer(location),
                left,
                right,
                kind,
                flags));
        }

        /// <summary>
        /// Determines a div/mul shift amount to convert div and mul operations into
        /// logical shift operations to improve performance.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="primitiveValue">The primitive value.</param>
        /// <returns>The converted shift amount.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueReference GetDivMulShiftAmount(
            Location location,
            PrimitiveValue primitiveValue) =>
            CreatePrimitiveValue(
                location,
                (int)Math.Log(
                    Math.Abs((double)primitiveValue.RawValue),
                    2.0));

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
