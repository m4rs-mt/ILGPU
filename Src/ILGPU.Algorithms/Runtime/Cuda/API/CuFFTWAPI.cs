// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTWAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the cuFFT API.
    /// </summary>
    [CLSCompliant(false)]
    public abstract partial class CuFFTWAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The cuFFT version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static CuFFTWAPI Create(CuFFTWAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static CuFFTWAPI CreateLatest()
        {
            Exception firstException = null;
            var versions = Enum.GetValues(typeof(CuFFTWAPIVersion));

            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = (CuFFTWAPIVersion)versions.GetValue(i);
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var ptr = api.malloc(new IntPtr(1));
                    api.free(ptr);
                    return api;
                }
                catch (Exception ex) when (
                    ex is DllNotFoundException ||
                    ex is EntryPointNotFoundException)
                {
                    firstException ??= ex;
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(CuFFTWAPI));
        }

        #endregion
    }
}

