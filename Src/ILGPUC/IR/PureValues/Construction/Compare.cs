// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
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

        // Compare integers cast to pointers by their native integer values
        if (left is IntAsPointerCast or NullValue { Type: PointerType } &&
            right is IntAsPointerCast or NullValue { Type: PointerType } &&
            (left is IntAsPointerCast || right is IntAsPointerCast))
        {
            return CreateCompare(
                location,
                CreateNativeInteger(location, left),
                CreateNativeInteger(location, right),
                kind,
                flags);
        }

        if (TryFoldArrayCompare(left, right, kind, out bool equal))
            return CreatePrimitiveValue(location, equal);

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

        // Two null pointers are equal, such as a pointer local that still holds
        // null compared with null
        if (left is NullValue { Type: PointerType } &&
            right is NullValue { Type: PointerType })
        {
            return CreatePrimitiveValue(
                location,
                kind is CompareKind.Equal or
                    CompareKind.LessEqual or
                    CompareKind.GreaterEqual);
        }

        // A null pointer is null in every address space: compared with a
        // pointer into a specific address space, such as &x == null, it takes
        // the type of that pointer. Pointers into different address spaces
        // (e.g. a pointer into a local structure whose type has been
        // specialized and a pointer loaded from memory) are compared as
        // generic pointers.
        if (left.Type is PointerType leftPointer &&
            right.Type is PointerType rightPointer &&
            !leftPointer.Equals(rightPointer) &&
            leftPointer.ElementType.Equals(rightPointer.ElementType))
        {
            if (right is NullValue)
            {
                right = CreateNull(location, leftPointer);
            }
            else if (left is NullValue)
            {
                left = CreateNull(location, rightPointer);
            }
            else
            {
                left = CreateAddressSpaceCast(
                    location,
                    left,
                    MemoryAddressSpace.Generic).AsNotNull();
                right = CreateAddressSpaceCast(
                    location,
                    right,
                    MemoryAddressSpace.Generic).AsNotNull();
            }
        }

        return Append(new CompareValue(
            GetInitializer(location),
            left,
            right,
            kind,
            flags));
    }

    /// <summary>
    /// Converts a native integer, which is a pointer or an integer, or a null
    /// pointer to the integer value of the native integer.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="value">The native integer or null pointer.</param>
    /// <returns>The integer value.</returns>
    private Value CreateNativeInteger(Location location, Value value)
    {
        var nativeIntType = ModuleBuilder.IntPointerType.BasicValueType;
        return value switch
        {
            NullValue => CreatePrimitiveValue(location, nativeIntType, 0),
            { Type: PointerType } =>
                CreatePointerAsIntCast(location, value, nativeIntType),
            _ => CreateConvert(location, value, nativeIntType),
        };
    }

    /// <summary>
    /// Folds comparisons of array references, which compare the addresses of
    /// the first elements of the arrays: every array allocation has a buffer
    /// of its own, also an empty one, while the null array has none.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="result">The result, if folded.</param>
    /// <returns>True if the comparison was folded.</returns>
    private static bool TryFoldArrayCompare(
        Value left,
        Value right,
        CompareKind kind,
        out bool result)
    {
        result = false;
        if (kind is not (CompareKind.Equal or CompareKind.NotEqual) ||
            !TryGetArrayReference(left, out var leftArray) ||
            !TryGetArrayReference(right, out var rightArray))
        {
            return false;
        }

        bool equal;
        switch (leftArray, rightArray)
        {
            case (NullValue, NullValue):
                equal = true;
                break;
            case (NullValue, ArrayValue):
            case (ArrayValue, NullValue):
                equal = false;
                break;
            case (ArrayValue leftValue, ArrayValue rightValue):
                equal = leftValue.BackBuffer == rightValue.BackBuffer;
                break;
            default:
                return false;
        }
        result = equal == (kind == CompareKind.Equal);
        return true;
    }

    /// <summary>
    /// Returns the array whose first element the given address points to, or
    /// the null pointer, such as the lowered address of the null array.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="array">The array or the null pointer, if any.</param>
    /// <returns>True if the address points to the first element.</returns>
    private static bool TryGetArrayReference(Value address, out Value array)
    {
        if (address is PointerCast cast)
            address = cast.Source;
        array = address;
        if (address is NullValue)
            return true;
        if (address is not LoadArrayElementAddress laea)
            return false;
        foreach (var dimension in laea.Dimensions)
        {
            if (!dimension.IsPrimitiveValue(0))
                return false;
        }
        array = laea.ArrayValue;
        return true;
    }
}
