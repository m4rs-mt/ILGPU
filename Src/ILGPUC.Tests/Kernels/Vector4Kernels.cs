// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector4Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Vector4"/> + <see cref="Matrix4x4"/>
/// surface. Vector4 is the densest packing (a single 128-bit lane) and is
/// the type the BCL hardware-intrinsic fast paths are designed around.
/// </summary>
static class Vector4Kernels
{
    public static void Vector4CtorFieldsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float x,
        float y,
        float z,
        float w)
    {
        var v = new Vector4(x, y, z, w);
        output[index] = v.X + v.Y + v.Z + v.W;
    }

    public static void Vector4AddKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        var r = a + b;
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4SubKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        var r = a - b;
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4MulVecKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        var r = a * b;
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4MulScalarKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        float s)
    {
        var r = a * s;
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4DivKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        var r = a / b;
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4DotKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        output[index] = Vector4.Dot(a, b);
    }

    public static void Vector4LengthKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v)
    {
        output[index] = v.Length();
    }

    public static void Vector4NormalizeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v)
    {
        var n = Vector4.Normalize(v);
        output[index] = n.X + n.Y + n.Z + n.W;
    }

    public static void Vector4TransformKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v,
        Matrix4x4 m)
    {
        var r = Vector4.Transform(v, m);
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4MinMaxKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b)
    {
        var lo = Vector4.Min(a, b);
        var hi = Vector4.Max(a, b);
        output[index] = lo.X + hi.W;
    }

    public static void Vector4ClampKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v,
        Vector4 lo,
        Vector4 hi)
    {
        var r = Vector4.Clamp(v, lo, hi);
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Vector4LerpKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 a,
        Vector4 b,
        float t)
    {
        var r = Vector4.Lerp(a, b, t);
        output[index] = r.X + r.Y + r.Z + r.W;
    }
}
