// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaVersionSet.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace CudaVersionUpdateTool.Abstractions
{
    [DebuggerDisplay("{InstructionSet}, {DriverVersion}, {Architecture}")]
    internal readonly struct CudaVersionSet
    {
        public readonly CudaInstructionSet InstructionSet;
        public readonly CudaDriverVersion DriverVersion;
        public readonly CudaArchitecture Architecture;

        public CudaVersionSet(
            CudaInstructionSet instructionSet,
            CudaDriverVersion driverVersion,
            CudaArchitecture architecture)
        {
            InstructionSet = instructionSet;
            DriverVersion = driverVersion;
            Architecture = architecture;
        }
    }
}
