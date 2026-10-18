// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector2Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Vector2"/> + <see cref="Matrix3x2"/>
/// surface. Each kernel exercises one operation
/// (or a small related group) so IR snapshots are readable per-op and
/// the BackendTests matrix gives per-op pass/fail per backend.
/// </summary>
static class Vector2Kernels
{
    public static void Vector2CtorFieldsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float x,
        float y)
    {
        var v = new Vector2(x, y);
        output[index] = v.X + v.Y;
    }

    public static void Vector2AddKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        var r = a + b;
        output[index] = r.X + r.Y;
    }

    public static void Vector2SubKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        var r = a - b;
        output[index] = r.X + r.Y;
    }

    public static void Vector2MulVecKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        var r = a * b;
        output[index] = r.X + r.Y;
    }

    public static void Vector2MulScalarKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        float s)
    {
        var r = a * s;
        output[index] = r.X + r.Y;
    }

    public static void Vector2DivKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        var r = a / b;
        output[index] = r.X + r.Y;
    }

    public static void Vector2DotKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        output[index] = Vector2.Dot(a, b);
    }

    public static void Vector2LengthKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v)
    {
        output[index] = v.Length();
    }

    public static void Vector2LengthSquaredKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v)
    {
        output[index] = v.LengthSquared();
    }

    public static void Vector2NormalizeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v)
    {
        var n = Vector2.Normalize(v);
        output[index] = n.X + n.Y;
    }

    public static void Vector2DistanceKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        output[index] = Vector2.Distance(a, b);
    }

    public static void Vector2TransformKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v,
        Matrix3x2 m)
    {
        var r = Vector2.Transform(v, m);
        output[index] = r.X + r.Y;
    }

    public static void Vector2MinMaxKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b)
    {
        var lo = Vector2.Min(a, b);
        var hi = Vector2.Max(a, b);
        output[index] = lo.X + hi.Y;
    }

    public static void Vector2ClampKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v,
        Vector2 lo,
        Vector2 hi)
    {
        var r = Vector2.Clamp(v, lo, hi);
        output[index] = r.X + r.Y;
    }

    public static void Vector2LerpKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 a,
        Vector2 b,
        float t)
    {
        var r = Vector2.Lerp(a, b, t);
        output[index] = r.X + r.Y;
    }

    public static void Vector2AbsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v)
    {
        var r = Vector2.Abs(v);
        output[index] = r.X + r.Y;
    }
}
