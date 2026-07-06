// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix4x4Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Matrix4x4"/> surface — the 3D affine /
/// projective transform matrix used by
/// <see cref="Vector3.Transform(Vector3, Matrix4x4)"/>. Covers the static
/// factory methods (<c>CreateRotationX</c>, <c>CreateLookAt</c>, etc.), the
/// matrix arithmetic operators and <c>Invert</c>, whose BCL implementations
/// use hardware intrinsics.
/// </summary>
static class Matrix4x4Kernels
{
    private static float Sum(in Matrix4x4 m) =>
        m.M11 + m.M12 + m.M13 + m.M14
        + m.M21 + m.M22 + m.M23 + m.M24
        + m.M31 + m.M32 + m.M33 + m.M34
        + m.M41 + m.M42 + m.M43 + m.M44;

    public static void Matrix4x4IdentityKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        output[index] = Sum(Matrix4x4.Identity);
    }

    public static void Matrix4x4CreateTranslationKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float tx,
        float ty,
        float tz)
    {
        var m = Matrix4x4.CreateTranslation(tx, ty, tz);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreateScaleKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float sx,
        float sy,
        float sz)
    {
        var m = Matrix4x4.CreateScale(sx, sy, sz);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreateRotationXKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float radians)
    {
        var m = Matrix4x4.CreateRotationX(radians);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreateRotationYKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float radians)
    {
        var m = Matrix4x4.CreateRotationY(radians);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreateRotationZKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float radians)
    {
        var m = Matrix4x4.CreateRotationZ(radians);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreatePerspectiveFovKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float fov,
        float aspect,
        float zNear,
        float zFar)
    {
        var m = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
        output[index] = Sum(m);
    }

    public static void Matrix4x4CreateLookAtKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 eye,
        Vector3 target,
        Vector3 up)
    {
        var m = Matrix4x4.CreateLookAt(eye, target, up);
        output[index] = Sum(m);
    }

    public static void Matrix4x4MultiplyKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a,
        Matrix4x4 b)
    {
        var m = a * b;
        output[index] = Sum(m);
    }

    public static void Matrix4x4TransposeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a)
    {
        var m = Matrix4x4.Transpose(a);
        output[index] = Sum(m);
    }

    public static void Matrix4x4DeterminantKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a)
    {
        output[index] = a.GetDeterminant();
    }

    public static void Matrix4x4InvertKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a)
    {
        if (Matrix4x4.Invert(a, out var inv))
            output[index] = Sum(inv);
        else
            output[index] = -1.0f;
    }

    public static void Matrix4x4DecomposeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a)
    {
        if (Matrix4x4.Decompose(a, out var scale, out var rot, out var trans))
            output[index] =
                scale.X + scale.Y + scale.Z +
                rot.X + rot.Y + rot.Z + rot.W +
                trans.X + trans.Y + trans.Z;
        else
            output[index] = -1.0f;
    }
}
