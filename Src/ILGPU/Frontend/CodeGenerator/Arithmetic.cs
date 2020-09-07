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

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Tries to map the given IR value representing a raw integer-based value size
        /// to a corresponding <see cref="BasicValueType"/> entry.
        /// </summary>
        /// <param name="value">The IR to map.</param>
        /// <param name="valueType">The determined basic-value type (if any).</param>
        /// <returns>
        /// True, if the given IR node could be mapped to a basic value type.
        /// </returns>
        private static bool TryGetBasicValueSize(
            Value value,
            out BasicValueType valueType)
        {
            valueType =
                value is PrimitiveValue size &&
                size.BasicValueType.IsInt()
                ? PrimitiveType.GetBasicValueTypeBySize(size.Int32Value)
                : BasicValueType.None;
            return valueType != BasicValueType.None;
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
                    result = Builder.CreateLoadElementAddress(
                        Location,
                        left,
                        right);
                }
                // Check whether this operation on pointer values can be converted
                // into a LEA instruction
                // FIXME: remove this code once we add additional LEA nodes
                else if (
                    kind == BinaryArithmeticKind.Add &&
                    right is BinaryArithmeticValue baseAddress &&
                    baseAddress.Kind == BinaryArithmeticKind.Mul &&
                    // Extract the element stride from the multiplication pattern
                    // (must be the right operand since we have moved all pointer
                    // values the left hand side by definition)
                    TryGetBasicValueSize(baseAddress.Right, out var strideType))
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
