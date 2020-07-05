// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Collections.Generic;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a PTX architecture.
    /// </summary>
    public enum PTXArchitecture
    {
        /// <summary>
        /// The 3.0 architecture.
        /// </summary>
        SM_30,

        /// <summary>
        /// The 3.2 architecture.
        /// </summary>
        SM_32,

        /// <summary>
        /// The 3.5 architecture.
        /// </summary>
        SM_35,

        /// <summary>
        /// The 3.7 architecture.
        /// </summary>
        SM_37,

        /// <summary>
        /// The 5.0 architecture.
        /// </summary>
        SM_50,

        /// <summary>
        /// The 5.2 architecture.
        /// </summary>
        SM_52,

        /// <summary>
        /// The 5.3 architecture.
        /// </summary>
        SM_53,

        /// <summary>
        /// The 6.0 architecture.
        /// </summary>
        SM_60,

        /// <summary>
        /// The 6.1 architecture.
        /// </summary>
        SM_61,

        /// <summary>
        /// The 6.2 architecture.
        /// </summary>
        SM_62,

        /// <summary>
        /// The 7.0 architecture.
        /// </summary>
        SM_70,

        /// <summary>
        /// The 7.2 architecture.
        /// </summary>
        SM_72,

        /// <summary>
        /// The 7.5 architecture.
        /// </summary>
        SM_75,

        /// <summary>
        /// The 8.0 architecture.
        /// </summary>
        SM_80,
    }

    /// <summary>
    /// Utilities for the <see cref="PTXArchitecture"/> enumeration.
    /// </summary>
    public static class PTXArchitectureUtils
    {
        #region Static

        /// <summary>
        /// Maps major and minor versions of Cuda devices to their corresponding PTX
        /// architecture.
        /// </summary>
        private static readonly Dictionary<long, PTXArchitecture> ArchitectureLookup =
            new Dictionary<long, PTXArchitecture>
        {
            { (3L << 32) | 0L, PTXArchitecture.SM_30 },
            { (3L << 32) | 2L, PTXArchitecture.SM_32 },
            { (3L << 32) | 5L, PTXArchitecture.SM_35 },
            { (3L << 32) | 7L, PTXArchitecture.SM_37 },

            { (5L << 32) | 0L, PTXArchitecture.SM_50 },
            { (5L << 32) | 2L, PTXArchitecture.SM_52 },
            { (5L << 32) | 3L, PTXArchitecture.SM_53 },

            { (6L << 32) | 0L, PTXArchitecture.SM_60 },
            { (6L << 32) | 1L, PTXArchitecture.SM_61 },
            { (6L << 32) | 2L, PTXArchitecture.SM_62 },

            { (7L << 32) | 0L, PTXArchitecture.SM_70 },
            { (7L << 32) | 2L, PTXArchitecture.SM_72 },
            { (7L << 32) | 5L, PTXArchitecture.SM_75 },

            { (8L << 32) | 0L, PTXArchitecture.SM_80 },
        };

        /// <summary>
        /// Resolves the PTX architecture for the given major and minor versions.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <returns>The resolved PTX version.</returns>
        public static PTXArchitecture GetArchitecture(int major, int minor)
        {
            if (!ArchitectureLookup.TryGetValue(
                ((long)major << 32) | (uint)minor, out PTXArchitecture result))
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedPTXArchitecture);
            }
            return result;
        }

        #endregion
    }
}
