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
            Block.PopArithmeticArgs(
                Location,
                convertFlags,
                out var left,
                out var right);
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
            var arithmetic = Builder.CreateArithmetic(
                Location,
                left,
                right,
                kind,
                arithmeticFlags);
            Block.Push(arithmetic);
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
