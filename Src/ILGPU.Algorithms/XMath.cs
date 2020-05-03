// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: XMath.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents basic math helpers for general double/float
    /// math operations that are supported on the CPU and the GPU.
    /// </summary>
    /// <remarks>
    /// NOTE: This class will be replaced by a specific fast-math implementations
    /// for cross platform (CPU/GPU) math functions in a future release.
    /// CAUTION: Therefore, these functions are currently not optimized for
    /// performance or precision in any way.
    /// </remarks>
    public static partial class XMath
    {
        /// <summary>
        /// The E constant.
        /// </summary>
        public const float E = 2.71828182f;

        /// <summary>
        /// The E constant.
        /// </summary>
        public const double ED = Math.E;

        /// <summary>
        /// The log2(E) constant.
        /// </summary>
        public const float Log2E = 1.44269504f;

        /// <summary>
        /// The log2(E) constant.
        /// </summary>
        public const double Log2ED = 1.4426950408889634;

        /// <summary>
        /// The 1/log2(2) constant.
        /// </summary>
        public const float OneOverLog2E = 1.0f / Log2E;

        /// <summary>
        /// The 1/log2(2) constant.
        /// </summary>
        public const double OneOverLog2ED = 1.0 / Log2ED;

        /// <summary>
        /// The log10(E) constant.
        /// </summary>
        public const float Log10E = 0.43429448f;

        /// <summary>
        /// The log10(E) constant.
        /// </summary>
        public const double Log10ED = 0.4342944819032518;

        /// <summary>
        /// The ln(2) constant.
        /// </summary>
        public const float Ln2 = 0.69314718f;

        /// <summary>
        /// The ln(2) constant.
        /// </summary>
        public const double Ln2D = 0.6931471805599453;

        /// <summary>
        /// The 1/ln(2) constant.
        /// </summary>
        public const float OneOverLn2 = 1.0f / Ln2;

        /// <summary>
        /// The 1/ln(2) constant.
        /// </summary>
        public const double OneOverLn2D = 1.0 / Ln2D;

        /// <summary>
        /// The ln(10) constant.
        /// </summary>
        public const float Ln10 = 2.30258509f;

        /// <summary>
        /// The ln(10) constant.
        /// </summary>
        public const double Ln10D = 2.3025850929940457;

        /// <summary>
        /// The 1/ln(10) constant.
        /// </summary>
        public const float OneOverLn10 = 1.0f / Ln10;

        /// <summary>
        /// The 1/ln(10) constant.
        /// </summary>
        public const double OneOverLn10D = 1.0 / Ln10D;

        /// <summary>
        /// The PI constant.
        /// </summary>
        public const float PI = 3.14159265f;

        /// <summary>
        /// The PI constant.
        /// </summary>
        public const double PID = Math.PI;

        /// <summary>
        /// The PI/2 constant.
        /// </summary>
        public const float PIHalf = PI / 2.0f;

        /// <summary>
        /// The PI/2 constant.
        /// </summary>
        public const double PIHalfD = PID / 2.0;

        /// <summary>
        /// The PI/4 constant.
        /// </summary>
        public const float PIFourth = PI / 4.0f;

        /// <summary>
        /// The PI/4 constant.
        /// </summary>
        public const double PIFourthD = PID / 4.0;

        /// <summary>
        /// The 1/PI constant.
        /// </summary>
        public const float OneOverPI = 1.0f / PI;

        /// <summary>
        /// The 2/PI constant.
        /// </summary>
        public const float TwoOverPI = 2.0f / PI;

        /// <summary>
        /// The sqrt(2) constant.
        /// </summary>
        public const float Sqrt2 = 1.41421356f;

        /// <summary>
        /// The 1/sqrt(2) constant.
        /// </summary>
        public const float OneOverSqrt2 = 1.0f / Sqrt2;

        /// <summary>
        /// The 1.0f / 3.0f constant.
        /// </summary>
        public const float OneThird = 1.0f / 3.0f;
    }
}
