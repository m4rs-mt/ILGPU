// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Realizes a convert instruction.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <param name="instructionFlags">The instruction flags.</param>
    private void MakeConvert(Type targetType, ILInstructionFlags instructionFlags)
    {
        var value = Block.Pop();
        var convertFlags = ConvertFlags.None;
        if (instructionFlags.HasFlags(ILInstructionFlags.Unsigned))
            convertFlags |= ConvertFlags.SourceUnsigned;
        if (targetType.IsUnsignedInt())
        {
            convertFlags |= ConvertFlags.SourceUnsigned;
            convertFlags |= ConvertFlags.TargetUnsigned;
        }
        var targetTypeNode = ModuleBuilder.CreateType(targetType);
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
    public Value CreateConversion(Value value, TypeValue targetType, ConvertFlags flags)
    {
        // The null reference of ldnull becomes the null array, such as a null
        // array stored to a local or passed to a parameter
        if (targetType is ArrayType && value is NullValue)
            return Builder.CreateNull(Location, targetType);

        if (value.Type is AddressSpaceType)
        {
            // Native integers are pointers: conversions to other primitive
            // types convert their integer values
            if (value.Type is PointerType && targetType is PrimitiveType)
            {
                var integer = Builder.CreatePointerAsIntCast(
                    Location,
                    value,
                    ModuleBuilder.IntPointerType.BasicValueType);
                return Builder.CreateConvert(Location, integer, targetType, flags);
            }
            if (targetType is not AddressSpaceType otherType)
            {
                throw Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedConversion,
                    value.Type,
                    targetType);
            }

            var source = value;
            value = Builder.CreateAddressSpaceCast(
                Location,
                value,
                otherType.AddressSpace);
            value = otherType is ViewType
                ? Builder.CreateViewCast(
                    Location,
                    value,
                    otherType.ElementType)
                : Builder.CreatePointerCast(
                    Location,
                    value,
                    otherType.ElementType);

            // Delegate handles keep their targets when they are converted, such
            // as when they are passed to a parameter of a delegate type
            if (value != source && source.Type is PointerType)
                _delegateAliases[value] = source;
            return value;
        }
        else if (
            targetType is PointerType { ElementType: VoidType } &&
            value.Type is PrimitiveType)
        {
            // Integer casts to pointers extend integers with their signs, while
            // conv.u extends them with zeros, which differs for negative values
            if ((flags & ConvertFlags.SourceUnsigned) != ConvertFlags.None &&
                value.BasicValueType < ModuleBuilder.IntPointerType.BasicValueType &&
                value is not PrimitiveValue { Int64Value: >= 0 })
            {
                value = Builder.CreateConvert(
                    Location,
                    value,
                    ModuleBuilder.IntPointerType,
                    flags);
            }
            return Builder.CreateIntAsPointerCast(Location, value);
        }
        else if (
            targetType is PointerType targetPointerType &&
            targetPointerType.ElementType is KindType)
        {
            // Must be a reflection array call
            // FIXME: note that we have to update this spot once we add support
            // for class types
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
