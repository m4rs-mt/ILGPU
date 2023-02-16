// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaInstructionSet.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace CudaVersionUpdateTool.Abstractions
{
    [DebuggerDisplay("ISA {Major}.{Minor}")]
    internal readonly struct CudaInstructionSet
    {
        public readonly int Major;
        public readonly int Minor;

        private CudaInstructionSet(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public static CudaInstructionSet Parse(string value)
        {
            var version = Version.Parse(value);
            return new CudaInstructionSet(version.Major, version.Minor);
        }

        public override string ToString() => $"{Major}.{Minor}";
    }
}
