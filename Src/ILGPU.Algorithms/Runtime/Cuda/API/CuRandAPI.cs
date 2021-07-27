// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuRandAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
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
                : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static CuRandAPI CreateLatest()
        {
            Exception firstException = null;
            var versions = Enum.GetValues(typeof(CuRandAPIVersion));

            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = (CuRandAPIVersion)versions.GetValue(i);
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

        #region Methods

        /// <summary>
        /// Creates a new GPU generator.
        /// </summary>
        public abstract CuRandStatus CreateGenerator(
            out IntPtr generator,
            CuRandRngType rngType);

        /// <summary>
        /// Creates a new CPU generator.
        /// </summary>
        public abstract CuRandStatus CreateGeneratorHost(
            out IntPtr generator,
            CuRandRngType rngType);

        /// <summary>
        /// Destroys the given generator.
        /// </summary>
        public abstract CuRandStatus DestoryGenerator(IntPtr generator);

        /// <summary>
        /// Determines the version of the API.
        /// </summary>
        public abstract CuRandStatus GetVersion(out int version);

        /// <summary>
        /// Sets the stream to use.
        /// </summary>
        public abstract CuRandStatus SetStream(
            IntPtr generator,
            IntPtr stream);

        /// <summary>
        /// Sets the stream seed.
        /// </summary>
        public abstract CuRandStatus SetSeed(
            IntPtr generator,
            long seed);

        /// <summary>
        /// Generates internal seeds.
        /// </summary>
        public abstract CuRandStatus GenerateSeeds(IntPtr generator);

        /// <summary>
        /// Generates uniform unsigned integers.
        /// </summary>
        public abstract CuRandStatus GenerateUInt(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        /// <summary>
        /// Generates uniform unsigned longs.
        /// </summary>
        public abstract CuRandStatus GenerateULong(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        /// <summary>
        /// Generates uniform unsigned floats.
        /// </summary>
        public abstract CuRandStatus GenerateUniformFloat(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        /// <summary>
        /// Generates uniform unsigned doubles.
        /// </summary>
        public abstract CuRandStatus GenerateUniformDouble(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        /// <summary>
        /// Generates normally distributed floats.
        /// </summary>
        public abstract CuRandStatus GenerateNormalFloat(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length,
            float mean,
            float stddev);

        /// <summary>
        /// Generates normally distributed doubles.
        /// </summary>
        public abstract CuRandStatus GenerateNormalDouble(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length,
            double mean,
            double stddev);

        #endregion
    }
}
