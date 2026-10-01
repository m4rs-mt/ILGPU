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
    /// Returns the sign-extended value of the given integer constant.
    /// </summary>
    /// <param name="value">The integer constant.</param>
    /// <returns>The value of the constant.</returns>
    private static long GetIntValue(PrimitiveValue value) =>
        value.BasicValueType == BasicValueType.Int64
        ? value.Int64Value
        : value.Int32Value;

    /// <summary>
    /// Returns the given index of a pointer moved by <paramref name="kind"/>.
    /// </summary>
    /// <param name="index">The index to move the pointer forward by.</param>
    /// <param name="kind">Add or Sub to move forward or backward.</param>
    /// <returns>The index to move the pointer by.</returns>
    private Value GetMoveIndex(Value index, BinaryArithmeticKind kind)
    {
        if (index is IntAsPointerCast cast)
            index = cast.Source;
        if (kind == BinaryArithmeticKind.Sub)
        {
            index = Builder.CreateArithmetic(
                Location,
                index,
                UnaryArithmeticKind.Neg).AsNotNull();
        }

        // Keep constant indices that fit 32 bits as 32-bit indices
        return index is PrimitiveValue constant &&
            constant.BasicValueType == BasicValueType.Int64 &&
            constant.Int64Value is >= int.MinValue and <= int.MaxValue
            ? Builder.CreatePrimitiveValue(Location, (int)constant.Int64Value)
            : index;
    }

    /// <summary>
    /// Returns the constant factor of the given scaled byte offset, such as 16
    /// of <c>(nint)i * 16</c> or <c>(nint)i &lt;&lt; 4</c>, or 0 if the offset
    /// is not scaled by a constant.
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The factor of the offset, or 0.</returns>
    private static long GetScale(BinaryArithmeticValue offset)
    {
        if (offset.Kind is not (BinaryArithmeticKind.Mul or BinaryArithmeticKind.Shl) ||
            offset.Right is not PrimitiveValue factor ||
            !offset.Left.BasicValueType.IsInt())
        {
            return 0;
        }
        long scale = GetIntValue(factor);
        if (offset.Kind == BinaryArithmeticKind.Shl)
            scale = scale is >= 0 and < 32 ? 1L << (int)scale : 0;
        return scale;
    }

    /// <summary>
    /// Returns true if the given byte offset is a multiple of the given element
    /// size that is known without a division: a constant, an offset scaled by
    /// a constant, or a sum or difference of such offsets.
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="elementSize">The element size in bytes.</param>
    /// <returns>True, if the offset is a multiple of the element size.</returns>
    private static bool IsElementOffset(Value offset, int elementSize)
    {
        switch (offset)
        {
            case PrimitiveValue constant when constant.BasicValueType.IsInt():
                return GetIntValue(constant) % elementSize == 0;
            case BinaryArithmeticValue
            {
                Kind: BinaryArithmeticKind.Add or BinaryArithmeticKind.Sub
            } binary:
                return IsElementOffset(binary.Left, elementSize) &&
                    IsElementOffset(binary.Right, elementSize);
            case BinaryArithmeticValue binary:
                long scale = GetScale(binary);
                return scale != 0 && scale % elementSize == 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Creates the element index of the given byte offset, which
    /// <see cref="IsElementOffset(Value, int)"/> accepts.
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="elementSize">The element size in bytes.</param>
    /// <returns>The element index.</returns>
    private Value CreateElementIndex(Value offset, int elementSize)
    {
        switch (offset)
        {
            case PrimitiveValue constant:
                return Builder.CreatePrimitiveValue(
                    Location,
                    constant.BasicValueType,
                    GetIntValue(constant) / elementSize);
            case BinaryArithmeticValue
            {
                Kind: BinaryArithmeticKind.Add or BinaryArithmeticKind.Sub
            } binary:
                return Builder.CreateArithmetic(
                    Location,
                    CreateElementIndex(binary.Left, elementSize),
                    CreateElementIndex(binary.Right, elementSize),
                    binary.Kind,
                    binary.Flags).AsNotNull();
            default:
                var scaled = (BinaryArithmeticValue)offset;
                long scale = GetScale(scaled);
                return scale == elementSize
                    ? scaled.Left
                    : Builder.CreateArithmetic(
                        Location,
                        scaled.Left,
                        Builder.CreatePrimitiveValue(
                            Location,
                            scaled.Left.BasicValueType,
                            scale / elementSize),
                        BinaryArithmeticKind.Mul,
                        scaled.Flags).AsNotNull();
        }
    }

    /// <summary>
    /// Creates the address of a pointer moved by the given byte offset: by
    /// elements if the offset is a multiple of the element size that is known
    /// without a division, such as <c>(nint)i * sizeof(T)</c>, or through a
    /// byte pointer otherwise, as in <c>(byte*)p + 1</c> on an int pointer p,
    /// whose IL does not convert the pointer, and for untyped addresses, such
    /// as <c>(nint)&amp;s.A + 4</c>. Pointers moved by bytes are cast back to
    /// their pointer types, so that conditions can select them together with
    /// other pointers of these types.
    /// </summary>
    /// <param name="pointer">The pointer to move.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="kind">Add or Sub to move forward or backward.</param>
    /// <returns>The moved pointer.</returns>
    private Value CreatePointerOffset(
        Value pointer,
        Value offset,
        BinaryArithmeticKind kind)
    {
        var elementType = pointer.GetTypeAs<PointerType>().ElementType;

        int elementSize = elementType is VoidType ? 1 : elementType.Size;
        if (elementSize > 0 &&
            (elementSize == 1 || IsElementOffset(offset, elementSize)))
        {
            var index = elementSize == 1
                ? offset
                : CreateElementIndex(offset, elementSize);
            if (elementType is not VoidType)
            {
                return Builder.CreateLoadElementAddress(
                    Location,
                    pointer,
                    GetMoveIndex(index, kind)).AsNotNull();
            }
        }

        var bytePointer = Builder.CreatePointerCast(
            Location,
            pointer,
            ModuleBuilder.GetPrimitiveType(BasicValueType.Int8));
        var moved = Builder.CreateLoadElementAddress(
            Location,
            bytePointer,
            GetMoveIndex(offset, kind));
        return Builder.CreatePointerCast(
            Location,
            moved,
            elementType).AsNotNull();
    }

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

            // Move the pointer by the byte offset, such as p + 4 or p - 4
            if (right.Type is not PointerType && isOffset)
            {
                result = CreatePointerOffset(left, right, kind);
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
