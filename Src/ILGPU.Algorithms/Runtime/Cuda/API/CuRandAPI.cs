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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using ILGPU.Algorithms.Resources;
using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the cuRAND API.
    /// </summary>
    public abstract class CuRandAPI : RuntimeAPI
    {
        #region Static

        /// <summary>
        /// Creates a new cuRAND API wrapper implementation for the current platform.
        /// </summary>
        /// <param name="context">The parent context.</param>
        /// <param name="version">The API version.</param>
        /// <returns>The created API instance.</returns>
        public static CuRandAPI Create(Context context, CuRandAPIVersion version)
        {
            int intVersion = (int)version;
            return context.RuntimeSystem.CreateDllWrapper<CuRandAPI>(
                windows: $"curand64_{intVersion}",
                linux: $"libcurand.so.{intVersion}",
                macos: $"libcurand.{intVersion}.dylib",
                ErrorMessages.NotSupportedCuRandAPI);
        }

        #endregion

        #region Instance

        protected CuRandAPI() { }

        #endregion

        #region RuntimeAPI

        public override bool IsSupported => true;

        public override bool Init() => true;

        #endregion

        #region Methods

        [DynamicImport("curandCreateGenerator")]
        public abstract CuRandStatus CreateGenerator(
            out IntPtr generator,
            CuRandRngType rngType);

        [DynamicImport("curandCreateGeneratorHost")]
        public abstract CuRandStatus CreateGeneratorHost(
            out IntPtr generator,
            CuRandRngType rngType);

        [DynamicImport("curandDestroyGenerator")]
        public abstract CuRandStatus DestoryGenerator(IntPtr generator);

        [DynamicImport("curandGetVersion")]
        public abstract CuRandStatus GetVersion(out int version);

        [DynamicImport("curandSetStream")]
        public abstract CuRandStatus SetStream(
            IntPtr generator,
            IntPtr stream);

        [DynamicImport("curandSetPseudoRandomGeneratorSeed")]
        public abstract CuRandStatus SetSeed(
            IntPtr generator,
            long seed);

        [DynamicImport("curandGenerateSeeds")]
        public abstract CuRandStatus GenerateSeeds(IntPtr generator);

        [DynamicImport("curandGenerate")]
        public abstract CuRandStatus GenerateUInt(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        [DynamicImport("curandGenerateLongLong")]
        public abstract CuRandStatus GenerateULong(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        [DynamicImport("curandGenerateUniform")]
        public abstract CuRandStatus GenerateUniformFloat(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        [DynamicImport("curandGenerateUniformDouble")]
        public abstract CuRandStatus GenerateUniformDouble(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length);

        [DynamicImport("curandGenerateNormal")]
        public abstract CuRandStatus GenerateNormalFloat(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length,
            float mean,
            float stddev);

        [DynamicImport("curandGenerateNormalDouble")]
        public abstract CuRandStatus GenerateNormalDouble(
            IntPtr generator,
            IntPtr outputPtr,
            IntPtr length,
            double mean,
            double stddev);

        #endregion
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
