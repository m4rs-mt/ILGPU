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
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// A native cuBlas API interface.
    /// </summary>
    abstract unsafe partial class CuBlasAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The cuBlas version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static CuBlasAPI Create(CuBlasAPIVersion version)
        {
            CuBlasAPI result;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    result = new WindowsAPI_V10();
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    result = new MacOSAPI_V10();
                else
                    result = new LinuxAPI_V10();
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException)
            {
                return null;
            }
            return result;
        }

        protected CuBlasAPI() { }

        #endregion
    }
}
