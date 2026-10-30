// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Realizes a compare instruction of the given type.
    /// </summary>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="instructionFlags">The instruction flags.</param>
    private void MakeCompare(
        CompareKind compareKind,
        ILInstructionFlags instructionFlags)
    {
        var value = CreateCompare(compareKind, instructionFlags);
        Block.Push(value);
    }

    /// <summary>
    /// Creates a compare instruction of the given type.
    /// </summary>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="instructionFlags">The instruction flags.</param>
    private Value CreateCompare(
        CompareKind compareKind,
        ILInstructionFlags instructionFlags)
    {
        var compareFlags = CompareFlags.None;
        if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
            compareFlags |= CompareFlags.UnsignedOrUnordered;
        return CreateCompare(compareKind, compareFlags);
    }

    /// <summary>
    /// Creates a compare instruction of the given type.
    /// </summary>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="flags">The comparison flags.</param>
    private Value CreateCompare(CompareKind compareKind, CompareFlags flags)
    {
        var right = Block.PopCompareValue(Location, ConvertFlags.None);
        var left = Block.PopCompareValue(Location, ConvertFlags.None);
        VerifyNoDelegateComparison(left, right);
        return CreateCompare(
            left,
            right,
            compareKind,
            flags);
    }

    /// <summary>
    /// Creates a compare instruction of the given type.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="flags">The comparison flags.</param>
    private Value CreateCompare(
        Value left,
        Value right,
        CompareKind compareKind,
        CompareFlags flags)
    {
        if (TryFoldReferenceCompare(left, right, compareKind, out bool constant))
            return Builder.CreatePrimitiveValue(Location, constant);

        var convertFlags = ConvertFlags.None;
        if ((flags & CompareFlags.UnsignedOrUnordered) ==
            CompareFlags.UnsignedOrUnordered)
        {
            convertFlags = ConvertFlags.SourceUnsigned;
        }
        if (IsNativeIntegerCompare(left, right, compareKind, flags))
        {
            var nativeIntType = ModuleBuilder.IntPointerType;
            left = CreateConversion(left, nativeIntType, convertFlags);
            right = CreateConversion(right, nativeIntType, convertFlags);
        }
        right = CreateConversion(right, left.Type, convertFlags);
        left = CreateConversion(left, right.Type, convertFlags);
        Location.Assert(left.BasicValueType == right.BasicValueType);
        return Builder.CreateCompare(
            Location,
            left,
            right,
            compareKind,
            flags).ThrowIfNull();
    }

    /// <summary>
    /// Returns true if the given comparison compares native integers, which are
    /// pointers in the IR, by their integer values: comparisons of pointers with
    /// integers and signed comparisons, which pointers never use.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="flags">The comparison flags.</param>
    /// <returns>True if the comparison compares integer values.</returns>
    private static bool IsNativeIntegerCompare(
        Value left,
        Value right,
        CompareKind compareKind,
        CompareFlags flags)
    {
        if (left.Type is not PointerType && right.Type is not PointerType)
            return false;
        return left.Type is PrimitiveType || right.Type is PrimitiveType ||
            IsIntegerPointer(left) || IsIntegerPointer(right) ||
            (flags & CompareFlags.UnsignedOrUnordered) == CompareFlags.None &&
            compareKind != CompareKind.Equal &&
            compareKind != CompareKind.NotEqual;
    }

    /// <summary>
    /// Returns true if the given value is an integer other than zero cast to a
    /// pointer: zeros cast to pointers are null pointers, which pointers compare
    /// with as pointers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True if the value is an integer cast to a pointer.</returns>
    private static bool IsIntegerPointer(Value value) =>
        value is IntAsPointerCast { Source: not PrimitiveValue { Int64Value: 0 } };

    /// <summary>
    /// Folds comparisons of boxed values with null, such as the
    /// <c>isinst; ldnull; cgt.un</c> sequence of a type test in Debug builds: a
    /// boxed value is never null.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="compareKind">The comparison kind.</param>
    /// <param name="result">The result, if folded.</param>
    /// <returns>True if the comparison was folded.</returns>
    private bool TryFoldReferenceCompare(
        Value left,
        Value right,
        CompareKind compareKind,
        out bool result)
    {
        result = false;
        bool leftBoxed = _boxedTypes.ContainsKey(left);
        bool rightBoxed = _boxedTypes.ContainsKey(right);
        bool leftKnown = leftBoxed || left is NullValue;
        bool rightKnown = rightBoxed || right is NullValue;
        if (!leftKnown || !rightKnown || !leftBoxed && !rightBoxed)
            return false;

        // Two boxes are the same object only if they come from the same box
        // instruction; their order as addresses is unknown
        if (leftBoxed && rightBoxed)
        {
            if (compareKind is not (CompareKind.Equal or CompareKind.NotEqual))
                return false;
            result = (left == right) == (compareKind == CompareKind.Equal);
            return true;
        }

        // Compare references as addresses: null is 0, a boxed value is not
        int leftAddress = leftBoxed ? 1 : 0;
        int rightAddress = rightBoxed ? 1 : 0;
        result = compareKind switch
        {
            CompareKind.Equal => leftAddress == rightAddress,
            CompareKind.NotEqual => leftAddress != rightAddress,
            CompareKind.GreaterThan => leftAddress > rightAddress,
            CompareKind.GreaterEqual => leftAddress >= rightAddress,
            CompareKind.LessThan => leftAddress < rightAddress,
            _ => leftAddress <= rightAddress,
        };
        return true;
    }
}
