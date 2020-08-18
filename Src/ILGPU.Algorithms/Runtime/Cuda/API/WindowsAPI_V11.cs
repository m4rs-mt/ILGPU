// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: WindowsAPI_V11.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// A Windows V11 cuBlas API.
    /// </summary>
    internal sealed unsafe partial class WindowsAPI_V11 : CuBlasAPI
    {
        #region Constants

        /// <summary>
        /// Represents the cuBlas library name.
        /// </summary>
        public const string LibName = "cublas64_11";

        #endregion
    }
}
