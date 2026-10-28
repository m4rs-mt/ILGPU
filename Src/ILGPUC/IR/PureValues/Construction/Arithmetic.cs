// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Creates a unary arithmetic operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="node">The operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>A node that represents the arithmetic operation.</returns>
    public Value? CreateArithmetic(
        Location location,
        Value? node,
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
    public Value? CreateArithmetic(
        Location location,
        Value? node,
        UnaryArithmeticKind kind,
        ArithmeticFlags flags)
    {
        if (node is null) return null;

        // Check for constants
        if (node is PrimitiveValue value)
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
    /// The inversions of the logical operations and comparisons created by this
    /// builder.
    /// </summary>
    private ConcurrentDictionary<Value, Value>? _inversions;

    /// <summary>
    /// Inverts a binary arithmetic value by pushing the negation into its
    /// operands (De Morgan). Every logical operation and comparison is inverted
    /// at most once per builder: predicates that share operations (for example
    /// the block predicates of if-converted short-circuit chains, which are
    /// negated by several select networks) form a DAG, which repeated or
    /// recursive expansions would turn into exponentially large trees.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="binary">The binary operation to invert.</param>
    /// <returns>The inverted binary operation.</returns>
    private Value? InvertBinaryArithmetic(
        Location location,
        BinaryArithmeticValue binary)
    {
        var inversions = LazyInitializer.EnsureInitialized(ref _inversions);

        Value? Invert(Value value)
        {
            if (value is not BinaryArithmeticValue nested ||
                !BinaryArithmeticValue.TryInvertLogical(nested.Kind, out var kind))
            {
                return CreateArithmetic(location, value, UnaryArithmeticKind.Not);
            }
            if (inversions.TryGetValue(nested, out var cached))
                return cached;

            var result = CreateArithmetic(
                nested.Location,
                Invert(nested.Left),
                Invert(nested.Right),
                kind,
                nested.Flags);
            if (result is not null)
                inversions.TryAdd(nested, result);
            return result;
        }

        return Invert(binary);
    }

    /// <summary>
    /// Inverts a compare value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="compareValue">The compare operation to invert.</param>
    /// <returns>The inverted compare value.</returns>
    private Value? InvertCompareValue(
        Location location,
        CompareValue? compareValue)
    {
        if (compareValue is null) return null;

        var inversions = LazyInitializer.EnsureInitialized(ref _inversions);
        if (inversions.TryGetValue(compareValue, out var cached))
            return cached;

        // Invert the operation kind and adjust the flags
        var flags = compareValue.Flags;
        var kind = CompareValue.Invert(
            compareValue.Kind,
            compareValue.Left.BasicValueType,
            compareValue.Right.BasicValueType,
            ref flags);

        // Create a new compare value using the updated kind and flags
        var result = CreateCompare(
            location,
            compareValue.Left,
            compareValue.Right,
            kind,
            flags);
        if (result is not null)
            inversions.TryAdd(compareValue, result);
        return result;
    }

    /// <summary>
    /// Creates a binary arithmetic operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>A node that represents the arithmetic operation.</returns>
    public Value? CreateArithmetic(
        Location location,
        Value? left,
        Value? right,
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
    public Value? CreateArithmetic(
        Location location,
        Value? left,
        Value? right,
        BinaryArithmeticKind kind,
        ArithmeticFlags flags)
    {
        if (left is null) return right;
        if (right is null) return left;

        VerifyBinaryArithmeticOperands(location, left, right, kind);

        Value? simplified;
        if (right is PrimitiveValue rightValue)
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

            // Combining nested constants reassociates the operation, which
            // changes the rounding of floating-point values
            if (left is BinaryArithmeticValue leftBinary &&
                leftBinary.Kind == kind &&
                (!leftBinary.IsFloatOperation || IsFastMath) &&
                leftBinary.Right is PrimitiveValue nestedRightValue &&
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
                (!rightBinary.IsFloatOperation || IsFastMath) &&
                rightBinary.Left is PrimitiveValue nestedLeftValue &&
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

        if ((simplified = LogicalSimplify(location, left, right, kind, flags)) != null)
            return simplified;

        return Append(new BinaryArithmeticValue(
            GetInitializer(location),
            left,
            right,
            kind,
            flags));
    }

    /// <summary>
    /// Returns true if both values are the same value or equal constants.
    /// </summary>
    private static bool AreSameOperand(Value first, Value second) =>
        first == second ||
        first is PrimitiveValue firstConstant &&
        second is PrimitiveValue secondConstant &&
        firstConstant.BasicValueType == secondConstant.BasicValueType &&
        firstConstant.RawValue == secondConstant.RawValue;

    /// <summary>
    /// Returns true if both boolean values are known to be equal: they are the
    /// same value or comparisons of the same operands.
    /// </summary>
    private static bool AreEquivalent(Value first, Value second) =>
        first == second ||
        first is CompareValue firstCompare &&
        second is CompareValue secondCompare &&
        firstCompare.Kind == secondCompare.Kind &&
        firstCompare.Flags == secondCompare.Flags &&
        AreSameOperand(firstCompare.Left, secondCompare.Left) &&
        AreSameOperand(firstCompare.Right, secondCompare.Right);

    /// <summary>
    /// Returns true if the given boolean values are known to be complements of
    /// each other: they are complementary (<see cref="AreComplementary"/>), or one
    /// is the inversion of the other created by this builder.
    /// </summary>
    private bool AreKnownComplements(Value first, Value second) =>
        AreComplementary(first, second) ||
        _inversions is { } inversions &&
        (inversions.TryGetValue(first, out var firstInverse) &&
            firstInverse == second ||
        inversions.TryGetValue(second, out var secondInverse) &&
            secondInverse == first);

    /// <summary>
    /// Returns true if the given boolean values are known to be complements
    /// of each other: one is the negation of the other, or both compare the
    /// same operands with inverse conditions (including unordered floats).
    /// </summary>
    private static bool AreComplementary(Value first, Value second)
    {
        if (second is UnaryArithmeticValue { Kind: UnaryArithmeticKind.Not } secondNot)
            return AreEquivalent(first, secondNot.Value);
        if (first is UnaryArithmeticValue { Kind: UnaryArithmeticKind.Not } firstNot)
            return AreEquivalent(firstNot.Value, second);
        if (first is not CompareValue firstCompare ||
            second is not CompareValue secondCompare)
        {
            return false;
        }

        var flags = firstCompare.Flags;
        var leftType = firstCompare.Left.BasicValueType;
        var rightType = firstCompare.Right.BasicValueType;
        var inverted = CompareValue.Invert(
            firstCompare.Kind,
            leftType,
            rightType,
            ref flags);
        if (AreSameOperand(secondCompare.Left, firstCompare.Left) &&
            AreSameOperand(secondCompare.Right, firstCompare.Right))
        {
            return secondCompare.Kind == inverted && secondCompare.Flags == flags;
        }

        // Compare the swapped form of the inverse: !(a < b) == (b <= a)
        var swapped = CompareValue.SwapOperands(
            inverted,
            leftType,
            rightType,
            ref flags);
        return AreSameOperand(secondCompare.Left, firstCompare.Right) &&
            AreSameOperand(secondCompare.Right, firstCompare.Left) &&
            secondCompare.Kind == swapped &&
            secondCompare.Flags == flags;
    }

    /// <summary>
    /// Simplifies logical and/or operations on booleans whose operands are
    /// equivalent or complementary: x | x → x, x | !x → true, x &amp; !x →
    /// false, including the commuted forms and one level of nesting.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">Operation flags.</param>
    /// <returns>The simplified value (if any).</returns>
    private Value? LogicalSimplify(
        Location location,
        Value left,
        Value right,
        BinaryArithmeticKind kind,
        ArithmeticFlags flags)
    {
        if (kind is not BinaryArithmeticKind.And and not BinaryArithmeticKind.Or ||
            left.BasicValueType != BasicValueType.Int1)
        {
            return null;
        }

        if (AreEquivalent(left, right))
            return left;
        if (AreKnownComplements(left, right))
            return CreatePrimitiveValue(location, kind == BinaryArithmeticKind.Or);

        return LogicalSimplifyFactor(left, right, kind)
            ?? LogicalSimplifyNested(location, left, right, kind, flags)
            ?? LogicalSimplifyNested(location, right, left, kind, flags);
    }

    /// <summary>
    /// Simplifies a logical operation of two dual logical operations that share
    /// an operand and differ in complementary operands, where op is the given
    /// kind and op' its dual: (x op' y) op (x op' !y) → x. If-conversion
    /// produces this shape for the predicate of a block that both arms of a
    /// branch join again.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>The simplified value (if any).</returns>
    private Value? LogicalSimplifyFactor(
        Value left,
        Value right,
        BinaryArithmeticKind kind)
    {
        var dual = BinaryArithmeticValue.InvertLogical(kind);
        if (left is not BinaryArithmeticValue leftBinary || leftBinary.Kind != dual ||
            right is not BinaryArithmeticValue rightBinary || rightBinary.Kind != dual)
        {
            return null;
        }

        var (a, b) = (leftBinary.Left, leftBinary.Right);
        var (c, d) = (rightBinary.Left, rightBinary.Right);
        if (AreEquivalent(a, c) && AreKnownComplements(b, d) ||
            AreEquivalent(a, d) && AreKnownComplements(b, c))
        {
            return a;
        }
        if (AreEquivalent(b, c) && AreKnownComplements(a, d) ||
            AreEquivalent(b, d) && AreKnownComplements(a, c))
        {
            return b;
        }
        return null;
    }

    /// <summary>
    /// Simplifies a logical operation whose nested operand is itself a logical
    /// operation, where op is the given kind and op' its dual:
    /// (x op y) op !x → true/false, (x op y) op x → x op y,
    /// (x op' y) op x → x and (x op' y) op !x → y op !x.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="nested">The (potentially) nested logical operation.</param>
    /// <param name="other">The other operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">Operation flags.</param>
    /// <returns>The simplified value (if any).</returns>
    private Value? LogicalSimplifyNested(
        Location location,
        Value nested,
        Value other,
        BinaryArithmeticKind kind,
        ArithmeticFlags flags)
    {
        if (nested is not BinaryArithmeticValue
            {
                Kind: BinaryArithmeticKind.And or BinaryArithmeticKind.Or
            } binary)
        {
            return null;
        }

        var (x, y) = (binary.Left, binary.Right);
        if (binary.Kind == kind)
        {
            if (AreKnownComplements(x, other) || AreKnownComplements(y, other))
                return CreatePrimitiveValue(location, kind == BinaryArithmeticKind.Or);
            if (AreEquivalent(x, other) || AreEquivalent(y, other))
                return nested;
            return null;
        }

        // Absorption
        if (AreEquivalent(x, other) || AreEquivalent(y, other))
            return other;

        // The complement of the other operand cannot change the result
        if (AreKnownComplements(x, other))
            return CreateArithmetic(location, y, other, kind, flags);
        if (AreKnownComplements(y, other))
            return CreateArithmetic(location, x, other, kind, flags);
        return null;
    }

    /// <summary>
    /// Determines a div/mul shift amount to convert div and mul operations into
    /// logical shift operations to improve performance.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="primitiveValue">The primitive value.</param>
    /// <returns>The converted shift amount.</returns>
    private PrimitiveValue GetDivMulShiftAmount(
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
    /// <param name="flags">Operation flags.</param>
    /// <returns>A node that represents the arithmetic operation.</returns>
    public Value? CreateArithmetic(
        Location location,
        Value? first,
        Value? second,
        Value? third,
        TernaryArithmeticKind kind,
        ArithmeticFlags flags)
    {
        if (first is null && second is null && third is null) return null;

        // Check for unification of the remaining operands
        if (first is null)
        {
            if (second is null) return third;
            if (third is null) return second;

            // Create binary arithmetic value
            return CreateArithmetic(
                location,
                second,
                third,
                TernaryArithmeticValue.GetRightBinaryKind(kind),
                flags);
        }
        else if (second is null)
        {
            if (third is null) return first;

            return CreateArithmetic(
                location,
                first,
                third,
                TernaryArithmeticValue.GetLeftBinaryKind(kind),
                flags);
        }
        else if (third is null)
        {
            return CreateArithmetic(
                location,
                first,
                second,
                TernaryArithmeticValue.GetLeftBinaryKind(kind),
                flags);
        }

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

        return Append(new TernaryArithmeticValue(
            GetInitializer(location),
            first,
            second,
            third,
            kind,
            flags));
    }
}
