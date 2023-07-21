// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuRandAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the cuRAND API.
    /// </summary>
    public abstract partial class CuRandAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The cuRand version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static CuRandAPI Create(CuRandAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
                ?? throw new DllNotFoundException(nameof(CuRandAPI))
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static CuRandAPI CreateLatest()
        {
            Exception? firstException = null;
#if NET5_0_OR_GREATER
            var versions = Enum.GetValues<CuRandAPIVersion>();
#else
            var versions = (CuRandAPIVersion[])Enum.GetValues(typeof(CuRandAPIVersion));
#endif
            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = versions[i];
                var api = CreateInternal(version);
                if (api != null)
                {
                    try
                    {
                        var status = api.GetVersion(out _);
                        if (status == CuRandStatus.CURAND_STATUS_SUCCESS)
                            return api;
                    }
                    catch (Exception ex) when (
                        ex is DllNotFoundException ||
                        ex is EntryPointNotFoundException)
                    {
                        firstException ??= ex;
                    }
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(CuRandAPI));
        }

        /// <summary>
        /// Constructs a new cuRAND API instance.
        /// </summary>
        protected CuRandAPI() { }

        #endregion
    }
}
