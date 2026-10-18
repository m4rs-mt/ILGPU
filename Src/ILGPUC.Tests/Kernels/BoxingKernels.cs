// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BoxingKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// A simple two-field struct used for box/unbox round trips. Two fields are
/// required: single-field structure types collapse to their field type in the
/// IR, which the frontend's box handler rejects (it requires an ObjectType).
/// </summary>
struct BoxedPair
{
    public int First;
    public int Second;
}

/// <summary>
/// Kernel definitions for boxing/unboxing tests. These exercise the IL
/// box/unbox.any instruction pair in the frontend (MakeBox/MakeUnbox).
/// </summary>
static class BoxingKernels
{
    /// <summary>
    /// Round-trips a value through object via box/unbox.any. The generic
    /// (T)(object)value cast is the canonical pattern that keeps the box and
    /// unbox.any instructions in the emitted IL.
    /// </summary>
    static T RoundTrip<T>(T value) where T : struct => (T)(object)value;

    /// <summary>
    /// Boxes a struct and unboxes it again within a single expression.
    /// </summary>
    public static void BoxUnboxStructKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        BoxedPair pair;
        pair.First = value;
        pair.Second = value * 2;
        var roundTripped = (BoxedPair)(object)pair;
        data[index] = roundTripped.First + roundTripped.Second;
    }

    /// <summary>
    /// Boxes and unboxes a struct through a generic helper method.
    /// </summary>
    public static void BoxUnboxGenericKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        BoxedPair pair;
        pair.First = value + 1;
        pair.Second = value * 3;
        var roundTripped = RoundTrip(pair);
        data[index] = roundTripped.First - roundTripped.Second;
    }
}
