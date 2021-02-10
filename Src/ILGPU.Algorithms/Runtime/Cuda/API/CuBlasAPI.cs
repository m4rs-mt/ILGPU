// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuBlasAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// A native cuBlas API interface.
    /// </summary>
    internal abstract unsafe partial class CuBlasAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The cuBlas version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static CuBlasAPI Create(CuBlasAPIVersion version)
        {
            try
            {
                return CreateInternal(version);
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException)
            {
                return null;
            }
        }

        protected CuBlasAPI() { }

        #endregion

        #region Methods
        public abstract CuBlasStatus Create(out IntPtr handle);

        public abstract CuBlasStatus GetVersion(
            IntPtr handle,
            out int version);

        public abstract CuBlasStatus Free(IntPtr handle);

        public abstract CuBlasStatus GetStream(
            IntPtr handle,
            out IntPtr stream);

        public abstract CuBlasStatus SetStream(
            IntPtr handle,
            IntPtr stream);

        public abstract CuBlasStatus GetPointerMode(
            IntPtr handle,
            out CuBlasPointerMode mode);

        public abstract CuBlasStatus SetPointerMode(
            IntPtr handle,
            CuBlasPointerMode mode);

        public abstract CuBlasStatus GetAtomicsMode(
            IntPtr handle,
            out CuBlasAtomicsMode mode);

        public abstract CuBlasStatus SetAtomicsMode(
            IntPtr handle,
            CuBlasAtomicsMode mode);

        public abstract CuBlasStatus GetMathMode(
            IntPtr handle,
            out CuBlasMathMode mode);

        public abstract CuBlasStatus SetMathMode(
            IntPtr handle,
            CuBlasMathMode mode);

        #endregion
    }
}
