// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
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
        /// <param name="targetType">The target type.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private void MakeConvert(
            Type targetType,
            ILInstructionFlags instructionFlags)
        {
            var value = Block.Pop();
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
            var targetTypeNode = Builder.CreateType(targetType);
            Block.Push(CreateConversion(
                value,
                targetTypeNode,
                convertFlags));
        }

        /// <summary>
        /// Coverts the given value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="flags">
        /// True, if the comparison should be forced to be unsigned.
        /// </param>
        public Value CreateConversion(
            Value value,
            TypeNode targetType,
            ConvertFlags flags)
        {
            if (value.Type is AddressSpaceType)
            {
                var otherType = targetType as AddressSpaceType;
                value = Builder.CreateAddressSpaceCast(
                    Location,
                    value,
                    otherType.AddressSpace);
                return otherType is ViewType
                    ? (Value)Builder.CreateViewCast(
                        Location,
                        value,
                        otherType.ElementType)
                    : (Value)Builder.CreatePointerCast(
                        Location,
                        value,
                        otherType.ElementType);
            }
            else if (
                targetType is PointerType targetPointerType &&
                targetPointerType.ElementType.IsRootType)
            {
                // Must be a reflection array call
                // FIXME: note that we have to update this spot once
                // we add support for class types
                Location.Assert(value.Type is StructureType);
                return value;
            }
            return Builder.CreateConvert(
                Location,
                value,
                targetType,
                flags);
        }
    }
}
