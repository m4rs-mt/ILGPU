// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: Rem.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rem(double x, double y) =>
            IntrinsicMath.CPUOnly.Rem(x, y);

        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rem(float x, float y) =>
            IntrinsicMath.CPUOnly.Rem(x, y);
    }
}
