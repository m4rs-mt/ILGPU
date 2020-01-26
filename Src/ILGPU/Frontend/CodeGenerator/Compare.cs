// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private static void MakeCompare(
            Block block,
            IRBuilder builder,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var value = CreateCompare(block, builder, compareKind, instructionFlags);
            block.Push(value);
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private static Value CreateCompare(
            Block block,
            IRBuilder builder,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var compareFlags = CompareFlags.None;
            if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
                compareFlags |= CompareFlags.UnsignedOrUnordered;
            return CreateCompare(block, builder, compareKind, compareFlags);
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="flags">The comparison flags.</param>
        private static Value CreateCompare(
            Block block,
            IRBuilder builder,
            CompareKind compareKind,
            CompareFlags flags)
        {
            var right = block.PopCompareValue(ConvertFlags.None);
            var left = block.PopCompareValue(ConvertFlags.None);
            return CreateCompare(builder, left, right, compareKind, flags);
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="compareKind">The comparison kind.</param>
        /// <param name="flags">The comparison flags.</param>
        private static Value CreateCompare(
            IRBuilder builder,
            Value left,
            Value right,
            CompareKind compareKind,
            CompareFlags flags)
        {
            var convertFlags = ConvertFlags.None;
            if ((flags & CompareFlags.UnsignedOrUnordered) == CompareFlags.UnsignedOrUnordered)
                convertFlags = ConvertFlags.SourceUnsigned;
            right = CreateConversion(builder, right, left.Type, convertFlags);
            left = CreateConversion(builder, left, right.Type, convertFlags);
            Debug.Assert(left.BasicValueType == right.BasicValueType);
            return builder.CreateCompare(left, right, compareKind, flags);
        }
    }
}
