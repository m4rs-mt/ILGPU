// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterleaveFieldsKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// AoS-of-SoA point with two
/// <see cref="System.Runtime.CompilerServices.InlineArrayAttribute"/>-backed
/// channels. Mirrors the manually-interleaved structure from
/// <c>Samples/InterleaveFields</c>.
/// </summary>
public struct InterleavedPoint4
{
    public IntInline4 X;
    public IntInline4 Y;
}

/// <summary>
/// Inline-array channel of four ints — used as a field of
/// <see cref="InterleavedPoint4"/>.
/// </summary>
[System.Runtime.CompilerServices.InlineArray(4)]
public struct IntInline4
{
    private int _element0;
}

/// <summary>
/// AoS-of-SoA write pattern with two <c>[InlineArray]</c>-backed channels.
/// Pins the cross-type struct-assignment shape (formerly B.2 in
/// <c>Src/plans/fix_samples.md</c>): the IR previously kept the un-flattened
/// sub-struct type for a multi-field <c>GetField</c> while the marshaled
/// parent had been flattened to primitive slots, producing
/// <c>error: no operator "=" matches these operands</c> from nvcc / hipcc /
/// clang on the generated source. Resolved by the multi-field <c>GetField</c>
/// struct-literal emit in <c>ExpressionEmitter.EmitGetField</c>; the kernel
/// stays here as a regression for that lowering path.
/// </summary>
static class InterleaveFieldsKernels
{
    public static void InterleaveFieldsKernel(
        Index1D index,
        ArrayView1D<InterleavedPoint4, Stride1D.Dense> dataView)
    {
        dataView[index].X[0] = index;
        dataView[index].X[1] = index + 1;
        dataView[index].X[2] = index + 2;
        dataView[index].X[3] = index + 3;
        dataView[index].Y[0] = index + 4;
        dataView[index].Y[1] = index + 5;
        dataView[index].Y[2] = index + 6;
        dataView[index].Y[3] = index + 7;
    }
}
