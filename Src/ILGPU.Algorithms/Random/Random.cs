// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright (c) 2017-2018 ILGPU Samples Project
//                                    www.ilgpu.net
//
// File: Random.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Represents useful helpers for random generators.
    /// </summary>
    internal static class RandomExtensions
    {
        /// <summary>
        /// 1.0 / int.MaxValue
        /// </summary>
        public const double InverseIntDoubleRange = 1.0 / int.MaxValue;
        /// <summary>
        /// 1.0 / int.MaxValue
        /// </summary>
        public const float InverseIntFloatRange = (float)InverseIntDoubleRange;

        /// <summary>
        /// 1.0 / long.MaxValue
        /// </summary>
        public const double InverseLongDoubleRange = 1.0 / long.MaxValue;

        /// <summary>
        /// 1.0 / long.MaxValue
        /// </summary>
        public const float InverseLongFloatRange = (float)InverseLongDoubleRange;
    }
}
