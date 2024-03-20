// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntrinsicMath.BinaryLog.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU
{
    partial class IntrinsicMath
    {
        /// <summary>
        /// Contains software implementation for Log with two parameters.
        /// </summary>
        internal static class BinaryLog
        {
            /// <summary>
            /// Implements Log with two parameters.
            /// </summary>
            public static double Log(double value, double newBase)
            {
                if (value < 0.0 ||
                    newBase < 0.0 ||
                    value != 1.0 && newBase == 0.0 ||
                    value != 1.0 && newBase == double.PositiveInfinity ||
                    CPUOnly.IsNaN(value) ||
                    CPUOnly.IsNaN(newBase) ||
                    newBase == 1.0)
                {
                    return double.NaN;
                }

                if (value == 0.0)
                {
                    if (0.0 < newBase && newBase < 1.0)
                        return double.PositiveInfinity;
                    else if (newBase > 1.0)
                        return double.NegativeInfinity;
                }

                if (value == double.PositiveInfinity)
                {
                    if (0.0 < newBase && newBase < 1.0)
                        return double.NegativeInfinity;
                    else if (newBase > 1.0)
                        return double.PositiveInfinity;
                }

                if (value == 1.0 &&
                    (newBase == 0.0 || newBase == double.PositiveInfinity))
                {
                    return 0.0;
                }

                return CPUOnly.Log(value) * CPUOnly.Rcp(CPUOnly.Log(newBase));
            }

            /// <summary>
            /// Implements Log with two parameters.
            /// </summary>
            public static float Log(float value, float newBase)
            {
                if (value < 0.0f ||
                    newBase < 0.0f ||
                    value != 1.0f && newBase == 0.0f ||
                    value != 1.0f && newBase == float.PositiveInfinity ||
                    CPUOnly.IsNaN(value) ||
                    CPUOnly.IsNaN(newBase) ||
                    newBase == 1.0f)
                {
                    return float.NaN;
                }

                if (value == 0.0f)
                {
                    if (0.0f < newBase && newBase < 1.0f)
                        return float.PositiveInfinity;
                    else if (newBase > 1.0f)
                        return float.NegativeInfinity;
                }

                if (value == float.PositiveInfinity)
                {
                    if (0.0f < newBase && newBase < 1.0f)
                        return float.NegativeInfinity;
                    else if (newBase > 1.0f)
                        return float.PositiveInfinity;
                }

                if (value == 1.0f &&
                    (newBase == 0.0f || newBase == float.PositiveInfinity))
                {
                    return 0.0f;
                }

                return CPUOnly.Log(value) * CPUOnly.Rcp(CPUOnly.Log(newBase));
            }
        }
    }
}
