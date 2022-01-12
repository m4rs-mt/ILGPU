// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTWStructs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles

namespace ILGPU.Runtime.Cuda
{
    public struct iodim
    {
        public int n;
        public int @is;
        public int os;
    }

    public struct iodim64
    {
        public IntPtr n;
        public IntPtr @is;
        public IntPtr os;
    }
}

#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore IDE1006 // Naming Styles
