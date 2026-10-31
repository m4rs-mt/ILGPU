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
    /// Tries to determine the stride in bytes of the given address offset: an
    /// element index multiplied or shifted by a constant.
    /// </summary>
    /// <param name="offset">The address offset.</param>
    /// <param name="stride">The determined stride (if any).</param>
    /// <returns>True, if the offset has a constant stride.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetStride(BinaryArithmeticValue offset, out int stride)
    {
        stride = offset.Right is PrimitiveValue size && size.BasicValueType.IsInt()
            ? offset.Kind switch
            {
                BinaryArithmeticKind.Mul => size.Int32Value,
                BinaryArithmeticKind.Shl => 1 << size.Int32Value,
                _ => 0,
            }
            : 0;
        return stride > 0;
    }

    /// <summary>
    /// Tries to convert the base address into a valid LEA operation.
    /// </summary>
    /// <param name="left">The left operand (the pointer to use).</param>
    /// <param name="baseAddress">The base address offset.</param>
    /// <param name="negate">True, if the offset is subtracted.</param>
    /// <param name="result">The result value (if any).</param>
    /// <returns>
    /// True, if the given pattern could be converted into a LEA node.
    /// </returns>
    private bool TryConvertIntoLoadElementAddress(
        Value left,
        BinaryArithmeticValue baseAddress,
        bool negate,
        [NotNullWhen(true)] out Value? result)
    {
        result = null;
        if (!TryGetStride(baseAddress, out int stride))
            return false;

        // Cast raw pointer into an appropriate target type, unless it points to
        // elements of the stride that no primitive type has, such as structures
        var index = baseAddress.Left;
        if (stride is 1 or 2 or 4 or 8)
        {
            var strideType = PrimitiveType.GetBasicValueTypeBySize(stride);
            left = Builder.CreatePointerCast(
                Location,
                left,
                ModuleBuilder.GetPrimitiveType(strideType)).AsNotNull();
        }
        else if (left.GetTypeAs<PointerType>().ElementType.Size != stride)
        {
            // Offset other pointers by bytes
            left = Builder.CreatePointerCast(
                Location,
                left,
                ModuleBuilder.GetPrimitiveType(BasicValueType.Int8)).AsNotNull();
            index = baseAddress.Type is PointerType
                ? Builder.CreatePointerAsIntCast(
                    Location,
                    baseAddress,
                    ModuleBuilder.IntPointerType.BasicValueType)
                : baseAddress;
        }

        if (negate)
        {
            index = Builder.CreateArithmetic(
                Location,
                index is IntAsPointerCast cast ? cast.Source : index,
                UnaryArithmeticKind.Neg).AsNotNull();
        }
        result = Builder.CreateLoadElementAddress(
            Location,
            left,
            index).AsNotNull();
        return true;
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
    /// Returns true if the given value is an address: a pointer that is not a
    /// native integer made from an integer, such as the result of an operation
    /// on native integers or the stride of an address computation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True if the value is an address.</returns>
    private static bool IsAddress(Value value) =>
        value.Type is PointerType &&
        value is not (IntAsPointerCast or NullValue or BinaryArithmeticValue);

    /// <summary>
    /// Returns the given address moved by the given number of bytes: untyped
    /// addresses move by bytes, as void pointers have no element size, and
    /// typed addresses by elements.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="offset">The offset in bytes.</param>
    /// <param name="negate">True to move the address backwards.</param>
    /// <returns>The moved address.</returns>
    private Value CreateByteOffset(Value address, Value offset, bool negate)
    {
        var pointerType = address.GetTypeAs<PointerType>();
        bool untyped = pointerType.ElementType is VoidType;
        if (!untyped)
        {
            offset = Builder.CreateArithmetic(
                Location,
                offset,
                Builder.CreateSizeOf(Location, pointerType.ElementType),
                BinaryArithmeticKind.Div).AsNotNull();
        }
        if (negate)
        {
            offset = Builder.CreateArithmetic(
                Location,
                offset,
                UnaryArithmeticKind.Neg).AsNotNull();
        }
        if (!untyped)
        {
            return Builder.CreateLoadElementAddress(
                Location,
                address,
                offset).AsNotNull();
        }

        var bytePointer = Builder.CreatePointerCast(
            Location,
            address,
            ModuleBuilder.GetPrimitiveType(BasicValueType.Int8));
        var movedPointer = Builder.CreateLoadElementAddress(
            Location,
            bytePointer,
            offset);
        return Builder.CreatePointerCast(
            Location,
            movedPointer,
            ModuleBuilder.VoidType).AsNotNull();
    }

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

            // Offsets are added to pointers or subtracted from pointers
            bool isOffset = kind == BinaryArithmeticKind.Add ||
                kind == BinaryArithmeticKind.Sub && !swapped;

            // Check for raw combinations of two pointer values
            if (
                right.Type is not PointerType &&
                // Check whether this can be safely converted into a LEA value
                isOffset)
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
                if (kind == BinaryArithmeticKind.Sub)
                {
                    leaIndex = Builder.CreateArithmetic(
                        Location,
                        leaIndex,
                        UnaryArithmeticKind.Neg);
                }
                result = Builder.CreateLoadElementAddress(
                    Location,
                    left,
                    leaIndex);
            }
            // Check whether this operation on pointer values can be converted
            // into a LEA instruction
            // FIXME: remove this code once we add additional LEA nodes
            else if (
                isOffset &&
                (right is IntAsPointerCast stride ? stride.Source : right)
                    is BinaryArithmeticValue baseAddress &&
                TryConvertIntoLoadElementAddress(
                    left,
                    baseAddress,
                    kind == BinaryArithmeticKind.Sub,
                    out var lea))
            {
                result = lea;
            }
            // Addresses moved by native integers made from integers, such as
            // (nint)&s.A + 4 or g + offset, stay addresses and keep their
            // address spaces
            else if (
                isOffset &&
                IsAddress(left) &&
                right is IntAsPointerCast byteOffset)
            {
                result = CreateByteOffset(
                    left,
                    byteOffset.Source,
                    kind == BinaryArithmeticKind.Sub);
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
