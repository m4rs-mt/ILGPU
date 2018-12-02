// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a convert instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private static void MakeConvert(
            Block block,
            IRBuilder builder,
            Type targetType,
            ILInstructionFlags instructionFlags)
        {
            var value = block.Pop();
            var convertFlags = ConvertFlags.None;
            if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
                convertFlags |= ConvertFlags.SourceUnsigned;
            if (instructionFlags.HasFlags(ILInstructionFlags.Overflow))
                convertFlags |= ConvertFlags.Overflow;
            if (targetType.IsUnsignedInt())
            {
                convertFlags |= ConvertFlags.SourceUnsigned;
                convertFlags |= ConvertFlags.TargetUnsigned;
            }
            var type = targetType.GetBasicValueType();
            var targetTypeNode = block.Builder.GetPrimitiveType(type);
            block.Push(CreateConversion(
                builder,
                value,
                targetTypeNode,
                convertFlags));
        }

        /// <summary>
        /// Conerts the given value to the target type.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="flags">True, if the comparison should be forced to be unsigned.</param>
        public static Value CreateConversion(
            IRBuilder builder,
            Value value,
            TypeNode targetType,
            ConvertFlags flags)
        {
            if (value.Type is AddressSpaceType pointerType)
            {
                var otherType = targetType as AddressSpaceType;
                if (otherType.AddressSpace == pointerType.AddressSpace)
                    return builder.CreatePointerCast(
                        value,
                        otherType.ElementType);
                else
                    return builder.CreateAddressSpaceCast(
                        value,
                        otherType.AddressSpace);
            }
            return builder.CreateConvert(value, targetType, flags);
        }

    }
}
