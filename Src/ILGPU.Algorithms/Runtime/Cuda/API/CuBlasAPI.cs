// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuBlasAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
        public static CuBlasAPI Create(CuBlasAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
                ?? throw new DllNotFoundException(nameof(CuBlasAPI))
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static CuBlasAPI CreateLatest()
        {
            Exception? firstException = null;
            var versions = Enum.GetValues<CuBlasAPIVersion>();
            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = versions[i];
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var status = api.Create(out var handle);
                    if (status == CuBlasStatus.CUBLAS_STATUS_SUCCESS)
                    {
                        api.Free(handle);
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

            throw firstException ?? new DllNotFoundException(nameof(CuBlasAPI));
        }

        /// <summary>
        /// Constructs a new cuBlas API instance.
        /// </summary>
        protected CuBlasAPI() { }

        #endregion
    }
}
