// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: LinuxAPI_V10.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// A Linux V10 cuBlas API.
    /// </summary>
    internal sealed unsafe partial class LinuxAPI_V10 : CuBlasAPI
    {
        #region Constants

        /// <summary>
        /// Represents the cuBlas library name.
        /// </summary>
        public const string LibName = "libcublas.so.10";

        #endregion
    }
}
