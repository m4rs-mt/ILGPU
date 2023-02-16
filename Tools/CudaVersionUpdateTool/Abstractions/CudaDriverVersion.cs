// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaDriverVersion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace CudaVersionUpdateTool.Abstractions
{
    [DebuggerDisplay("CUDA {Major}.{Minor}")]
    internal readonly struct CudaDriverVersion
    {
        public readonly int Major;
        public readonly int Minor;

        private CudaDriverVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public static CudaDriverVersion Parse(string value)
        {
            var version = Version.Parse(value);
            return new CudaDriverVersion(version.Major, version.Minor);
        }

        public override string ToString() => $"{Major}.{Minor}";
    }
}
