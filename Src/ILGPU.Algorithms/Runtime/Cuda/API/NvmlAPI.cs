// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the NVML API.
    /// </summary>
    [CLSCompliant(false)]
    public abstract partial class NvmlAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The NVML version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static NvmlAPI Create(NvmlAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static NvmlAPI CreateLatest()
        {
            Exception firstException = null;
            var versions = Enum.GetValues(typeof(NvmlAPIVersion));

            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = (NvmlAPIVersion)versions.GetValue(i);
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var status = api.DeviceGetCount(out _);
                    if (status == NvmlReturn.NVML_SUCCESS ||
                        status == NvmlReturn.NVML_ERROR_UNINITIALIZED)
                    {
                        return api;
                    }
                }
                catch (Exception ex) when (
                    ex is DllNotFoundException ||
                    ex is EntryPointNotFoundException)
                {
                    firstException ??= ex;
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(NvmlAPI));
        }

        #endregion
    }
}
