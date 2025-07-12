// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using System.Diagnostics;

namespace ILGPUC.Backends.PTX;

partial class PTXCodeGenerator
{
    /// <summary>
    /// Generates code for <see cref="LoadElementAddress"/> values.
    /// </summary>
    public void GenerateCode(LoadElementAddress value)
    {
        var elementIndex = LoadPrimitive(value.Offset);
        var targetAddressRegister = AllocateHardware(value);
        Debug.Assert(value.IsPointerAccess, "Invalid pointer access");

        var address = LoadPrimitive(value.Source);
        var sourceType = value.Source.Type.AsNotNullCast<AddressSpaceType>();
        var elementSize = sourceType.ElementType.Size;

        if (value.Is32BitAccess)
        {
            // Perform two efficient operations TODO
            var offsetRegister = AllocatePlatformRegister(out RegisterDescription _);
            using (var command = BeginCommand(
                PTXInstructions.GetLEAMulOperation(PointerArithmeticType)))
            {
                command.AppendArgument(offsetRegister);
                command.AppendArgument(elementIndex);
                command.AppendConstant(elementSize);
            }

            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Add,
                    PointerArithmeticType,
                    Capabilities,
                    false)))
            {
                command.AppendArgument(targetAddressRegister);
                command.AppendArgument(address);
                command.AppendArgument(offsetRegister);
            }

            FreeRegister(offsetRegister);
        }
        else
        {
            // Use an efficient MAD instruction to compute the effective address
            using var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    TernaryArithmeticKind.MultiplyAdd,
                    PointerArithmeticType));
            command.AppendArgument(targetAddressRegister);
            command.AppendArgument(elementIndex);
            command.AppendConstant(elementSize);
            command.AppendArgument(address);
        }
    }

    /// <summary>
    /// Creates an address-space cast conversion.
    /// </summary>
    /// <param name="sourceRegister">The source register.</param>
    /// <param name="targetRegister">The target register.</param>
    /// <param name="sourceAddressSpace">The source address space.</param>
    /// <param name="targetAddressSpace">The target address space.</param>
    private void CreateAddressSpaceCast(
        PrimitiveRegister sourceRegister,
        HardwareRegister targetRegister,
        MemoryAddressSpace sourceAddressSpace,
        MemoryAddressSpace targetAddressSpace)
    {
        var toGeneric = targetAddressSpace == MemoryAddressSpace.Generic;
        var addressSpaceOperation = PTXInstructions.GetAddressSpaceCast(toGeneric);
        var addressSpaceOperationSuffix =
            PTXInstructions.GetAddressSpaceCastSuffix(PointerBasicValueType);

        using var command = BeginCommand(addressSpaceOperation);
        command.AppendAddressSpace(
            toGeneric ? sourceAddressSpace : targetAddressSpace);
        command.AppendSuffix(addressSpaceOperationSuffix);
        command.AppendArgument(targetRegister);
        command.AppendArgument(sourceRegister);
    }

    /// <summary>
    /// Generates code for <see cref="AddressSpaceCast"/> values.
    /// </summary>
    public void GenerateCode(AddressSpaceCast value)
    {
        var sourceType = value.SourceType.As<AddressSpaceType>(value);
        var targetAddressRegister = AllocateHardware(value);
        value.Assert(value.IsPointerCast);

        var address = LoadPrimitive(value.Value);
        CreateAddressSpaceCast(
            address,
            targetAddressRegister,
            sourceType.AddressSpace,
            value.TargetAddressSpace);
    }
}
