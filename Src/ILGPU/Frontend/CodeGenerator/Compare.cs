// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;

namespace ILGPU.Frontend
{
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
            var convertFlags = ConvertFlags.None;
            if ((flags & CompareFlags.UnsignedOrUnordered) ==
                CompareFlags.UnsignedOrUnordered)
            {
                convertFlags = ConvertFlags.SourceUnsigned;
            }
            right = CreateConversion(right, left.Type, convertFlags);
            left = CreateConversion(left, right.Type, convertFlags);
            Location.Assert(left.BasicValueType == right.BasicValueType);
            return Builder.CreateCompare(
                Location,
                left,
                right,
                compareKind,
                flags);
        }
    }
}
