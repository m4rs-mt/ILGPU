// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vec128Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

#if NET7_0_OR_GREATER

namespace ILGPU.Backends.Velocity.Vec128
{
    partial class Vec128Operations
    {
        #region Rcp

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<float> RcpImpl(Vector128<float> value) =>
            Vector128.Create(1.0f) / value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<double> RcpImpl(Vector128<double> value) =>
            Vector128.Create(1.0) / value;

        #endregion

        #region Thread Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BarrierPopCount32Scalar(
            Vector128<int> mask,
            Vector128<int> warp) =>
            -Vector.Sum(AndI32(mask, warp).AsVector());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<int> BarrierPopCount32(
            Vector128<int> mask,
            Vector128<int> warp) =>
            Vector128.Create(BarrierPopCount32Scalar(mask, warp));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BarrierPopCount64Scalar(
            Vector128<int> mask,
            (Vector128<long>, Vector128<long>) warp)
        {
            var parts = AndI64(Convert32To64I(mask), warp);
            return -(int)(
                Vector.Sum(parts.Item1.AsVector()) -
                Vector.Sum(parts.Item2.AsVector()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector128<long>, Vector128<long>) BarrierPopCount64(
            Vector128<int> mask,
            (Vector128<long>, Vector128<long>) warp) =>
            FromScalarI64(BarrierPopCount64Scalar(mask, warp));

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector128<int> BarrierAnd32(
            Vector128<int> mask,
            Vector128<int> warp,
            int groupSize) =>
            BarrierPopCount32Scalar(mask, warp) == groupSize
                ? Vector128<int>.AllBitsSet
                : Vector128<int>.Zero;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Vector128<long>, Vector128<long>) BarrierAnd64(
            Vector128<int> mask,
            (Vector128<long>, Vector128<long>) warp,
            int groupSize) =>
            BarrierPopCount64Scalar(mask, warp) == groupSize
                ? (Vector128<long>.AllBitsSet, Vector128<long>.AllBitsSet)
                : (Vector128<long>.Zero, Vector128<long>.Zero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<int> BarrierOr32(
            Vector128<int> mask,
            Vector128<int> warp) =>
            BarrierPopCount32Scalar(mask, warp) != 0
                ? Vector128<int>.AllBitsSet
                : Vector128<int>.Zero;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector128<long>, Vector128<long>) BarrierOr64(
            Vector128<int> mask,
            (Vector128<long>, Vector128<long>) warp) =>
            BarrierPopCount64Scalar(mask, warp) != 0
                ? (Vector128<long>.AllBitsSet, Vector128<long>.AllBitsSet)
                : (Vector128<long>.Zero, Vector128<long>.Zero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<int> Broadcast32(
            Vector128<int> _,
            Vector128<int> value,
            Vector128<int> sourceLane)
        {
            // Extract base source lane
            int sourceLaneIndex = sourceLane.GetElement(0);

            // Broadcast without referring to the current mask
            return Broadcast32Internal(value, sourceLaneIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Broadcast32Internal(
            Vector128<int> value,
            int sourceLaneIndex) =>
            Vector128.Create(value[sourceLaneIndex]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector128<long>, Vector128<long>) Broadcast64(
            Vector128<int> _,
            (Vector128<long>, Vector128<long>) value,
            (Vector128<long>, Vector128<long>) sourceLane) =>
            throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<int> Shuffle32(
            Vector128<int> _,
            Vector128<int> value,
            Vector128<int> sourceLanes)
        {
            var lanes = MinI32(
                MaxI32(sourceLanes, Vector128<int>.Zero),
                WarpSizeM1Vector);

            int value0 = value.GetElement(lanes.GetElement(0));
            int value1 = value.GetElement(lanes.GetElement(1));
            int value2 = value.GetElement(lanes.GetElement(2));
            int value3 = value.GetElement(lanes.GetElement(3));

            return Vector128.Create(value0, value1, value2, value3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector128<long>, Vector128<long>) Shuffle64(
            Vector128<int> _,
            (Vector128<long>, Vector128<long>) value,
            Vector128<int> sourceLanes) =>
            throw new NotImplementedException();

        #endregion
    }
}

#endif
