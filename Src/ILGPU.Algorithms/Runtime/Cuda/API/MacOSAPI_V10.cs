// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: MacOSAPI_V10.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// A MacOS V10 cuBlas API.
    /// </summary>
    internal sealed unsafe partial class MacOSAPI_V10 : CuBlasAPI
    {
        #region Constants

        /// <summary>
        /// Represents the cuBlas library name.
        /// </summary>
        public const string LibName = "libcublas.10.dylib";

        #endregion
    }
}
