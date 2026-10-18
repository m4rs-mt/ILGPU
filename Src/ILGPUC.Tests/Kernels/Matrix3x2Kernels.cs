// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix3x2Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Matrix3x2"/> surface — the 2D affine
/// transform matrix. Exercises the static factory methods and matrix
/// algebra primitives.
/// </summary>
static class Matrix3x2Kernels
{
    private static float Sum(in Matrix3x2 m) =>
        m.M11 + m.M12 + m.M21 + m.M22 + m.M31 + m.M32;

    public static void Matrix3x2IdentityKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        output[index] = Sum(Matrix3x2.Identity);
    }

    public static void Matrix3x2CreateTranslationKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float tx,
        float ty)
    {
        var m = Matrix3x2.CreateTranslation(tx, ty);
        output[index] = Sum(m);
    }

    public static void Matrix3x2CreateRotationKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float radians)
    {
        var m = Matrix3x2.CreateRotation(radians);
        output[index] = Sum(m);
    }

    public static void Matrix3x2CreateScaleKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float sx,
        float sy)
    {
        var m = Matrix3x2.CreateScale(sx, sy);
        output[index] = Sum(m);
    }

    public static void Matrix3x2MultiplyKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix3x2 a,
        Matrix3x2 b)
    {
        var m = a * b;
        output[index] = Sum(m);
    }

    public static void Matrix3x2InvertKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix3x2 a)
    {
        if (Matrix3x2.Invert(a, out var inv))
            output[index] = Sum(inv);
        else
            output[index] = -1.0f;
    }
}
