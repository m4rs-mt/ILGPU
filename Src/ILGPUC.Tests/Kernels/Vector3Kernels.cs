// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector3Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Vector3"/> + <see cref="Matrix4x4"/>
/// surface, centred on <see cref="Vector3TransformKernel"/>.
/// </summary>
static class Vector3Kernels
{
    public static void Vector3CtorFieldsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float x,
        float y,
        float z)
    {
        var v = new Vector3(x, y, z);
        output[index] = v.X + v.Y + v.Z;
    }

    public static void Vector3AddKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var r = a + b;
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3SubKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var r = a - b;
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3MulVecKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var r = a * b;
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3MulScalarKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        float s)
    {
        var r = a * s;
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3DivKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var r = a / b;
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3DotKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        output[index] = Vector3.Dot(a, b);
    }

    public static void Vector3CrossKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var r = Vector3.Cross(a, b);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3LengthKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v)
    {
        output[index] = v.Length();
    }

    public static void Vector3NormalizeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v)
    {
        var n = Vector3.Normalize(v);
        output[index] = n.X + n.Y + n.Z;
    }

    public static void Vector3DistanceKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        output[index] = Vector3.Distance(a, b);
    }

    /// <summary>
    /// Transforms a point by a matrix. The .NET 8+ implementation of
    /// <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> calls
    /// <c>System.Runtime.Intrinsics.Vector128.AsVector128(Vector4)</c>,
    /// which has no managed body, so the call is remapped.
    /// </summary>
    public static void Vector3TransformKernel(
        Index1D index,
        ArrayView1D<Vector3, Stride1D.Dense> cloud,
        Matrix4x4 transform)
    {
        if (cloud[index] == Vector3.Zero)
            return;
        cloud[index] = Vector3.Transform(cloud[index], transform);
    }

    public static void Vector3TransformNormalKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v,
        Matrix4x4 m)
    {
        var r = Vector3.TransformNormal(v, m);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3ReflectKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v,
        Vector3 n)
    {
        var r = Vector3.Reflect(v, n);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3MinMaxKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b)
    {
        var lo = Vector3.Min(a, b);
        var hi = Vector3.Max(a, b);
        output[index] = lo.X + hi.Z;
    }

    public static void Vector3ClampKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v,
        Vector3 lo,
        Vector3 hi)
    {
        var r = Vector3.Clamp(v, lo, hi);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3LerpKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a,
        Vector3 b,
        float t)
    {
        var r = Vector3.Lerp(a, b, t);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector3AbsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v)
    {
        var r = Vector3.Abs(v);
        output[index] = r.X + r.Y + r.Z;
    }

    /// <summary>
    /// Computes the matrix math of <see cref="Vector3TransformKernel"/> by
    /// hand, as a reference that uses only field loads and arithmetic.
    /// </summary>
    public static void Vector3TransformManualKernel(
        Index1D index,
        ArrayView1D<Vector3, Stride1D.Dense> cloud,
        Matrix4x4 t)
    {
        if (cloud[index] == Vector3.Zero)
            return;
        var p = cloud[index];
        cloud[index] = new Vector3(
            p.X * t.M11 + p.Y * t.M21 + p.Z * t.M31 + t.M41,
            p.X * t.M12 + p.Y * t.M22 + p.Z * t.M32 + t.M42,
            p.X * t.M13 + p.Y * t.M23 + p.Z * t.M33 + t.M43);
    }
}
