// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPU.Frontend
{
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
            out Value result)
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
                var targetElementType = Builder.GetPrimitiveType(strideType);
                left = Builder.CreatePointerCast(
                    Location,
                    left,
                    targetElementType);
                result = Builder.CreateLoadElementAddress(
                    Location,
                    left,
                    baseAddress.Left);
                return true;
            }
            result = null;
            return false;
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

            ValueReference result = default;
            if (Block.PopArithmeticArgs(
                Location,
                convertFlags,
                out var left,
                out var right) == Block.ArithmeticOperandKind.Pointer)
            {
                // This is a pointer access
                bool isLeftPointer = left.Type.IsPointerType;
                if (!isLeftPointer)
                    Utilities.Swap(ref left, ref right);

                // Check for raw combinations of two pointer values
                if (
                    !right.Type.IsPointerType &&
                    // Check whether this can be safely converted into a LEA value
                    kind == BinaryArithmeticKind.Add)
                {
                    // Check the size of the element type and devide the raw offset
                    // by the element size to retrieve the actual element index.
                    var elementType = left.Type.As<PointerType>(Location).ElementType;
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
                    right is BinaryArithmeticValue baseAddress &&
                    TryConvertIntoLoadElementAddress(left, baseAddress, out var lea))
                {
                    result = lea;
                }
            }

            if (!result.IsValid)
            {
                switch (kind)
                {
                    case BinaryArithmeticKind.Shl:
                    case BinaryArithmeticKind.Shr:
                        // Convert right operand to 32bits
                        right = CreateConversion(
                            right,
                            Builder.GetPrimitiveType(BasicValueType.Int32),
                            convertFlags);
                        break;
                }
                result = Builder.CreateArithmetic(
                    Location,
                    left,
                    right,
                    kind,
                    arithmeticFlags);
            }
            Block.Push(result);
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
            var arithmetic = Builder.CreateArithmetic(
                Location,
                value,
                kind);
            Block.Push(arithmetic);
        }
    }
}
