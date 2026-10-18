// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Tries to map the given IR value representing a raw integer-based value size
    /// to a corresponding <see cref="BasicValueType"/> entry.
    /// </summary>
    /// <param name="value">The IR to map.</param>
    /// <param name="shiftValue">The base value for shift operations.</param>
    /// <param name="valueType">The determined basic-value type (if any).</param>
    /// <returns>
    /// True, if the given IR node could be mapped to a basic value type.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetBasicValueSize(
        Value value,
        int? shiftValue,
        out BasicValueType valueType)
    {
        valueType =
            value is PrimitiveValue size &&
            size.BasicValueType.IsInt()
            ? PrimitiveType.GetBasicValueTypeBySize(
                shiftValue is null
                ? size.Int32Value
                : shiftValue.Value << size.Int32Value)
            : BasicValueType.None;
        return valueType != BasicValueType.None;
    }

    /// <summary>
    /// Tries to convert the base address into a valid LEA operation.
    /// </summary>
    /// <param name="left">The left operand (the pointer to use).</param>
    /// <param name="baseAddress">The base address offset.</param>
    /// <param name="result">The result value (if any).</param>
    /// <returns>
    /// True, if the given pattern could be converted into a LEA node.
    /// </returns>
    private bool TryConvertIntoLoadElementAddress(
        Value left,
        BinaryArithmeticValue baseAddress,
        [NotNullWhen(true)] out Value? result)
    {
        if (
            // Check multiplications
            baseAddress.Kind == BinaryArithmeticKind.Mul &&
            // Extract the element stride from the multiplication pattern
            // (must be the right operand since we have moved all pointer
            // values the left hand side by definition)
            TryGetBasicValueSize(baseAddress.Right, null, out var strideType) ||

            // Check shift-based address computations
            baseAddress.Kind == BinaryArithmeticKind.Shl &&
            // Extract the element stride from the shift pattern
            TryGetBasicValueSize(baseAddress.Right, 1, out strideType))
        {
            // Cast raw pointer into an appropriate target type
            var targetElementType = ModuleBuilder.GetPrimitiveType(strideType);
            left = Builder.CreatePointerCast(
                Location,
                left,
                targetElementType).AsNotNull();
            result = Builder.CreateLoadElementAddress(
                Location,
                left,
                baseAddress.Left).AsNotNull();
            return true;
        }
        result = null;
        return false;
    }

    /// <summary>
    /// Returns true if the given operation computes the stride of an address
    /// computation: an integer extended to a native integer, multiplied or
    /// shifted by a constant, which keeps the pointer type of native integers
    /// until it is added to a pointer (see
    /// <see cref="TryConvertIntoLoadElementAddress"/>).
    /// </summary>
    /// <param name="kind">The kind of the arithmetic operation.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if the operation computes the stride of an address.</returns>
    private bool IsAddressStride(
        BinaryArithmeticKind kind,
        Value left,
        Value right) =>
        (kind == BinaryArithmeticKind.Mul || kind == BinaryArithmeticKind.Shl) &&
        left is IntAsPointerCast index &&
        (index.Source is ConvertValue ||
        index.Source.BasicValueType != ModuleBuilder.IntPointerType.BasicValueType) &&
        right is PrimitiveValue;

    /// <summary>
    /// Realizes an arithmetic operation.
    /// </summary>
    /// <param name="kind">The kind of the arithmetic operation.</param>
    /// <param name="instruction">The current IL instruction.</param>
    private void MakeArithmetic(
        BinaryArithmeticKind kind,
        ILInstruction instruction)
    {
        var arithmeticFlags = ArithmeticFlags.None;
        var convertFlags = ConvertFlags.None;
        if (instruction.HasFlags(ILInstructionFlags.Overflow))
            arithmeticFlags |= ArithmeticFlags.Overflow;
        if (instruction.HasFlags(ILInstructionFlags.Unsigned))
        {
            convertFlags |= ConvertFlags.TargetUnsigned;
            arithmeticFlags |= ArithmeticFlags.Unsigned;
        }

        Value? result = null;
        bool isNativeInteger = false;
        if (Block.PopArithmeticArgs(
            Location,
            convertFlags,
            out var left,
            out var right) == Block.ArithmeticOperandKind.Pointer)
        {
            // This is a pointer access
            bool swapped = Utilities.Swap(
                left.Type is not PointerType,
                ref left,
                ref right);

            // Check for raw combinations of two pointer values
            if (
                right.Type is not PointerType &&
                // Check whether this can be safely converted into a LEA value
                kind == BinaryArithmeticKind.Add)
            {
                // Check the size of the element type and divide the raw offset
                // by the element size to retrieve the actual element index.
                var elementType = left.GetTypeAs<PointerType>().ElementType;
                var elementSize = Builder.CreateSizeOf(Location, elementType);

                // Create the actual address computation
                var leaIndex = Builder.CreateArithmetic(
                    Location,
                    right,
                    elementSize,
                    BinaryArithmeticKind.Div);
                result = Builder.CreateLoadElementAddress(
                    Location,
                    left,
                    leaIndex);
            }
            // Check whether this operation on pointer values can be converted
            // into a LEA instruction
            // FIXME: remove this code once we add additional LEA nodes
            else if (
                kind == BinaryArithmeticKind.Add &&
                (right is IntAsPointerCast stride ? stride.Source : right)
                    is BinaryArithmeticValue baseAddress &&
                TryConvertIntoLoadElementAddress(left, baseAddress, out var lea))
            {
                result = lea;
            }
            // Native integers are pointers: all other operations on them
            // compute on their integer values, except for the strides of
            // address computations, which are added to pointers
            else if (!IsAddressStride(kind, left, right))
            {
                Utilities.Swap(swapped, ref left, ref right);
                var nativeIntType = ModuleBuilder.IntPointerType;
                left = CreateConversion(left, nativeIntType, convertFlags);
                if (kind != BinaryArithmeticKind.Shl &&
                    kind != BinaryArithmeticKind.Shr)
                {
                    right = CreateConversion(right, nativeIntType, convertFlags);
                }
                isNativeInteger = true;
            }
        }

        if (result is null)
        {
            switch (kind)
            {
                case BinaryArithmeticKind.Add:
                case BinaryArithmeticKind.Sub:
                case BinaryArithmeticKind.Mul:
                    PrimitiveValue? const0 = null;
                    PrimitiveValue? const1 = null;
                    // Convert the left operand, if required
                    if (left.BasicValueType == BasicValueType.Int1)
                    {
                        const0 = Builder.CreatePrimitiveValue(Location, 0);
                        const1 = Builder.CreatePrimitiveValue(Location, 1);
                        left = Builder.CreatePredicate(
                            Location,
                            left,
                            const1,
                            const0);
                    }

                    // Convert the right operand, if required
                    if (right.BasicValueType == BasicValueType.Int1)
                    {
                        const0 ??= Builder.CreatePrimitiveValue(Location, 0);
                        const1 ??= Builder.CreatePrimitiveValue(Location, 1);
                        right = Builder.CreatePredicate(
                            Location,
                            right,
                            const1,
                            const0);
                    }
                    break;
                case BinaryArithmeticKind.Shl:
                case BinaryArithmeticKind.Shr:
                    // Convert right operand to 32bits
                    right = CreateConversion(
                        right,
                        ModuleBuilder.GetPrimitiveType(BasicValueType.Int32),
                        convertFlags);
                    break;
            }
            result = Builder.CreateArithmetic(
                Location,
                left,
                right,
                kind,
                arithmeticFlags);
            if (isNativeInteger)
                result = Builder.CreateIntAsPointerCast(Location, result);
        }
        Block.Push(result.AsNotNull());
    }

    /// <summary>
    /// Realizes an arithmetic operation.
    /// </summary>
    /// <param name="kind">The kind of the arithmetic operation.</param>
    private void MakeArithmetic(UnaryArithmeticKind kind)
    {
        var value = Block.PopCompareOrArithmeticValue(
            Location,
            ConvertFlags.None);

        // Native integers are pointers: operations on them compute on their
        // integer values
        bool isNativeInteger = value.Type is PointerType;
        if (isNativeInteger)
        {
            value = CreateConversion(
                value,
                ModuleBuilder.IntPointerType,
                ConvertFlags.None);
        }
        var arithmetic = Builder.CreateArithmetic(
            Location,
            value,
            kind);
        if (isNativeInteger)
            arithmetic = Builder.CreateIntAsPointerCast(Location, arithmetic);
        Block.Push(arithmetic.AsNotNull());
    }
}
