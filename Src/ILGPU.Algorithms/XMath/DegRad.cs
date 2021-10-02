// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: DegRad.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Converts the given value in degrees to radians.
        /// </summary>
        /// <param name="degrees">The value in degrees to convert.</param>
        /// <returns>The converted value in radians.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DegToRad(double degrees)
        {
            const double _PIOver180 = PID / 180.0;
            return degrees * _PIOver180;
        }

        /// <summary>
        /// Converts the given value in degrees to radians.
        /// </summary>
        /// <param name="degrees">The value in degrees to convert.</param>
        /// <returns>The converted value in radians.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DegToRad(float degrees)
        {
            const float _PIOver180 = PI / 180.0f;
            return degrees * _PIOver180;
        }

        /// <summary>
        /// Converts the given value in radians to degrees.
        /// </summary>
        /// <param name="radians">The value in radians to convert.</param>
        /// <returns>The converted value in degrees.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RadToDeg(double radians)
        {
            const double _180OverPi = 180.0 / PID;
            return radians * _180OverPi;
        }

        /// <summary>
        /// Converts the given value in radians to degrees.
        /// </summary>
        /// <param name="radians">The value in radians to convert.</param>
        /// <returns>The converted value in degrees.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RadToDeg(float radians)
        {
            const float _180OverPi = 180.0f / PI;
            return radians * _180OverPi;
        }
    }
}
