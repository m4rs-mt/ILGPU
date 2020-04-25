// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        /// <summary cref="IBackendCodeGenerator.GenerateCode(LoadElementAddress)"/>
        public void GenerateCode(LoadElementAddress value)
        {
            var elementIndex = LoadPrimitive(value.ElementIndex);
            var targetAddressRegister = AllocatePlatformRegister(
                value,
                out RegisterDescription _);
            Debug.Assert(value.IsPointerAccess, "Invalid pointer access");

            var address = LoadPrimitive(value.Source);
            var sourceType = value.Source.Type as AddressSpaceType;
            var elementSize = sourceType.ElementType.Size;
            var offsetRegister = AllocatePlatformRegister(out RegisterDescription _);
            using (var command = BeginCommand(
                PTXInstructions.GetLEAMulOperation(Backend.PointerArithmeticType)))
            {
                command.AppendArgument(offsetRegister);
                command.AppendArgument(elementIndex);
                command.AppendConstant(elementSize);
            }

            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Add,
                    Backend.PointerArithmeticType,
                    false)))
            {
                command.AppendArgument(targetAddressRegister);
                command.AppendArgument(address);
                command.AppendArgument(offsetRegister);
            }

            FreeRegister(offsetRegister);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(AddressSpaceCast)"/>
        public void GenerateCode(AddressSpaceCast value)
        {
            var sourceType = value.SourceType as AddressSpaceType;
            var targetAdressRegister = AllocatePlatformRegister(
                value,
                out RegisterDescription _);
            Debug.Assert(value.IsPointerCast, "Invalid pointer access");

            var address = LoadPrimitive(value.Value);
            var toGeneric = value.TargetAddressSpace == MemoryAddressSpace.Generic;
            var addressSpaceOperation = PTXInstructions.GetAddressSpaceCast(toGeneric);
            var addressSpaceOperationSuffix =
                PTXInstructions.GetAddressSpaceCastSuffix(Backend);

            using (var command = BeginCommand(addressSpaceOperation))
            {
                command.AppendAddressSpace(
                    toGeneric ? sourceType.AddressSpace : value.TargetAddressSpace);
                command.AppendSuffix(addressSpaceOperationSuffix);
                command.AppendArgument(targetAdressRegister);
                command.AppendArgument(address);
            }
        }
    }
}
