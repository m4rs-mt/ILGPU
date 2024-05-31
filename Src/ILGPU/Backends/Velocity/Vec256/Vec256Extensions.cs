// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vec256Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#if NET7_0_OR_GREATER

namespace ILGPU.Backends.Velocity.Vec256
{
    partial class Vec256Operations
    {
        #region Rcp

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<float> RcpImpl(Vector256<float> value) =>
            Avx.IsSupported
                ? Avx.Reciprocal(value)
                : Vector256.Create(1.0f) / value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<double> RcpImpl(Vector256<double> value) =>
            Vector256.Create(1.0) / value;

        #endregion

        #region Rscrt

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<float> RsqrtImpl(Vector256<float> value) =>
            Avx.IsSupported
                ? Avx.ReciprocalSqrt(value)
                : Vector256.Create(1.0f) / value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<double> RsqrtImpl(Vector256<double> value) =>
            Vector256.Create(1.0) / value;

        #endregion

        #region FMA

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> FMAImpl(
            Vector256<int> a,
            Vector256<int> b,
            Vector256<int> c) => a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<uint> FMAImpl(
            Vector256<uint> a,
            Vector256<uint> b,
            Vector256<uint> c) => a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<long> FMAImpl(
            Vector256<long> a,
            Vector256<long> b,
            Vector256<long> c) => a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ulong> FMAImpl(
            Vector256<ulong> a,
            Vector256<ulong> b,
            Vector256<ulong> c) => a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<float> FMAImpl(
            Vector256<float> a,
            Vector256<float> b,
            Vector256<float> c) =>
            Fma.IsSupported
                ? Fma.MultiplyAdd(a, b, c)
                : a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<double> FMAImpl(
            Vector256<double> a,
            Vector256<double> b,
            Vector256<double> c) =>
            Fma.IsSupported
                ? Fma.MultiplyAdd(a, b, c)
                : a * b + c;

        #endregion

        #region Thread Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BarrierPopCount32Scalar(
            Vector256<int> mask,
            Vector256<int> warp) =>
            -Vector.Sum(AndI32(mask, warp).AsVector());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> BarrierPopCount32(
            Vector256<int> mask,
            Vector256<int> warp) =>
            Vector256.Create(BarrierPopCount32Scalar(mask, warp));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BarrierPopCount64Scalar(
            Vector256<int> mask,
            (Vector256<long>, Vector256<long>) warp)
        {
            var parts = AndI64(Convert32To64I(mask), warp);
            return -(int)(
                Vector.Sum(parts.Item1.AsVector()) -
                Vector.Sum(parts.Item2.AsVector()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector256<long>, Vector256<long>) BarrierPopCount64(
            Vector256<int> mask,
            (Vector256<long>, Vector256<long>) warp) =>
            FromScalarI64(BarrierPopCount64Scalar(mask, warp));

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector256<int> BarrierAnd32(
            Vector256<int> mask,
            Vector256<int> warp,
            int groupSize) =>
            BarrierPopCount32Scalar(mask, warp) == groupSize
                ? Vector256<int>.AllBitsSet
                : Vector256<int>.Zero;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Vector256<long>, Vector256<long>) BarrierAnd64(
            Vector256<int> mask,
            (Vector256<long>, Vector256<long>) warp,
            int groupSize) =>
            BarrierPopCount64Scalar(mask, warp) == groupSize
                ? (Vector256<long>.AllBitsSet, Vector256<long>.AllBitsSet)
                : (Vector256<long>.Zero, Vector256<long>.Zero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> BarrierOr32(
            Vector256<int> mask,
            Vector256<int> warp) =>
            BarrierPopCount32Scalar(mask, warp) != 0
                ? Vector256<int>.AllBitsSet
                : Vector256<int>.Zero;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector256<long>, Vector256<long>) BarrierOr64(
            Vector256<int> mask,
            (Vector256<long>, Vector256<long>) warp) =>
            BarrierPopCount64Scalar(mask, warp) != 0
                ? (Vector256<long>.AllBitsSet, Vector256<long>.AllBitsSet)
                : (Vector256<long>.Zero, Vector256<long>.Zero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> Broadcast32(
            Vector256<int> _,
            Vector256<int> value,
            Vector256<int> sourceLane)
        {
            // Extract base source lane
            int sourceLaneIndex = sourceLane.GetElement(0);

            // Broadcast without referring to the current mask
            return Broadcast32Internal(value, sourceLaneIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Broadcast32Internal(
            Vector256<int> value,
            int sourceLaneIndex) =>
            Vector256.Create(value[sourceLaneIndex]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector256<long>, Vector256<long>) Broadcast64(
            Vector256<int> _,
            (Vector256<long>, Vector256<long>) value,
            (Vector256<long>, Vector256<long>) sourceLane) =>
            throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> Shuffle32(
            Vector256<int> _,
            Vector256<int> value,
            Vector256<int> sourceLanes)
        {
            var lanes = MinI32(
                MaxI32(sourceLanes, Vector256<int>.Zero),
                WarpSizeM1Vector);

            int value0 = value.GetElement(lanes.GetElement(0));
            int value1 = value.GetElement(lanes.GetElement(1));
            int value2 = value.GetElement(lanes.GetElement(2));
            int value3 = value.GetElement(lanes.GetElement(3));
            int value4 = value.GetElement(lanes.GetElement(4));
            int value5 = value.GetElement(lanes.GetElement(5));
            int value6 = value.GetElement(lanes.GetElement(6));
            int value7 = value.GetElement(lanes.GetElement(7));

            return Vector256.Create(
                value0, value1, value2, value3,
                value4, value5, value6, value7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector256<long>, Vector256<long>) Shuffle64(
            Vector256<int> _,
            (Vector256<long>, Vector256<long>) value,
            Vector256<int> sourceLanes) =>
            throw new NotImplementedException();

        #endregion
    }
}

#endif
