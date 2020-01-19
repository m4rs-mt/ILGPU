// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2020 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: NativeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Native methods for the <see cref="CuBlas"/> class.
    /// </summary>
    static unsafe partial class NativeMethods
    {
        #region Constants

        /// <summary>
        /// Represents the cuBlas library name.
        /// </summary>
        public const string LibName = "cublas64_10";

        #endregion

        #region Context

        [DllImport(LibName, EntryPoint = "cublasCreate_v2")]
        public static extern CuBlasStatus Create(out IntPtr handle);

        [DllImport(LibName, EntryPoint = "cublasGetVersion_v2")]
        public static extern CuBlasStatus GetVersion(IntPtr handle, out int version);

        [DllImport(LibName, EntryPoint = "cublasDestroy_v2")]
        public static extern CuBlasStatus Free(IntPtr handle);

        [DllImport(LibName, EntryPoint = "cublasGetStream_v2")]
        public static extern CuBlasStatus GetStream(IntPtr handle, out IntPtr stream);

        [DllImport(LibName, EntryPoint = "cublasSetStream_v2")]
        public static extern CuBlasStatus SetStream(IntPtr handle, IntPtr stream);

        [DllImport(LibName, EntryPoint = "cublasGetPointerMode_v2")]
        public static extern CuBlasStatus GetPointerMode(IntPtr handle, out CuBlasPointerMode mode);

        [DllImport(LibName, EntryPoint = "cublasSetPointerMode_v2")]
        public static extern CuBlasStatus SetPointerMode(IntPtr handle, CuBlasPointerMode mode);

        [DllImport(LibName, EntryPoint = "cublasGetAtomicsMode")]
        public static extern CuBlasStatus GetAtomicsMode(IntPtr handle, out CuBlasAtomicsMode mode);

        [DllImport(LibName, EntryPoint = "cublasSetAtomicsMode")]
        public static extern CuBlasStatus SetAtomicsMode(IntPtr handle, CuBlasAtomicsMode mode);

        [DllImport(LibName, EntryPoint = "cublasGetMathMode")]
        public static extern CuBlasStatus GetMathMode(IntPtr handle, out CuBlasMathMode mode);

        [DllImport(LibName, EntryPoint = "cublasSetStream_v2")]
        public static extern CuBlasStatus SetMathMode(IntPtr handle, CuBlasMathMode mode);

        #endregion
    }
}

