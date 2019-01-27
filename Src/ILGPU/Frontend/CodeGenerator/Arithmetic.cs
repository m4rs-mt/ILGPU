// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes an arithmetic operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="kind">The kind of the arithmetic operation.</param>
        /// <param name="instruction">The current IL instruction.</param>
        private static void MakeArithmetic(
            Block block,
            IRBuilder builder,
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
            block.PopArithmeticArgs(convertFlags, out Value left, out Value right);
            switch (kind)
            {
                case BinaryArithmeticKind.Shl:
                case BinaryArithmeticKind.Shr:
                    // Convert right operand to 32bits
                    right = CreateConversion(
                        builder,
                        right,
                        builder.GetPrimitiveType(BasicValueType.Int32),
                        convertFlags);
                    break;
            }
            var arithmetic = builder.CreateArithmetic(left, right, kind, arithmeticFlags);
            block.Push(arithmetic);
        }

        /// <summary>
        /// Realizes an arithmetic operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="kind">The kind of the arithmetic operation.</param>
        private static void MakeArithmetic(
            Block block,
            IRBuilder builder,
            UnaryArithmeticKind kind)
        {
            var value = block.PopCompareOrArithmeticValue(ConvertFlags.None);
            var arithmetic = builder.CreateArithmetic(value, kind);
            block.Push(arithmetic);
        }
    }
}
