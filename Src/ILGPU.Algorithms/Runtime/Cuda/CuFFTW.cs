// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTW.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wrapper over cuFFTW to simplify integration with ILGPU.
    /// </summary>
    [CLSCompliant(false)]
    public sealed partial class CuFFTW
    {
        /// <summary>
        /// Constructs a new CuFFTW instance.
        /// </summary>
        public CuFFTW()
            : this(default)
        { }

        /// <summary>
        /// Constructs a new CuFFTW instance.
        /// </summary>
        public CuFFTW(CuFFTWAPIVersion? version)
        {
            API = CuFFTWAPI.Create(version);
        }

        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public CuFFTWAPI API { get; }
    }
}
