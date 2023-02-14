// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;

namespace CudaVersionUpdateTool.Abstractions
{
    [DebuggerDisplay("SM {Major}.{Minor}")]
    internal readonly struct CudaArchitecture
    {
        public readonly int Major;
        public readonly int Minor;

        private CudaArchitecture(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public static CudaArchitecture Parse(string value)
        {
            var version = Version.Parse(value);
            return new CudaArchitecture(version.Major, version.Minor);
        }

        public override string ToString() => $"{Major}.{Minor}";
    }
}
