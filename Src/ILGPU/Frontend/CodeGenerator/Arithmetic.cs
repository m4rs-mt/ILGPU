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
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
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

            ValueReference result;
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

                if (kind != BinaryArithmeticKind.Add || right.Type.IsPointerType)
                {
                    throw Location.GetNotSupportedException(
                        ErrorMessages.NotSupportedArithmeticArgumentType,
                        kind);
                }
                result = Builder.CreateLoadElementAddress(
                    Location,
                    left,
                    right);
            }
            else
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
