// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: QuaternionKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Quaternion"/> surface, whose BCL
/// implementation fans out into
/// <c>System.Runtime.Intrinsics.Vector128&lt;T&gt;</c>.
/// </summary>
static class QuaternionKernels
{
    private static float Sum(in Quaternion q) => q.X + q.Y + q.Z + q.W;

    public static void QuaternionCtorFieldsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float x, float y, float z, float w)
    {
        var q = new Quaternion(x, y, z, w);
        output[index] = q.X + q.Y + q.Z + q.W;
    }

    public static void QuaternionCtorVector3Kernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v, float scalar)
    {
        var q = new Quaternion(v, scalar);
        output[index] = Sum(q);
    }

    public static void QuaternionIdentityKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        output[index] = Sum(Quaternion.Identity);
    }

    public static void QuaternionAddKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        var r = a + b;
        output[index] = Sum(r);
    }

    public static void QuaternionSubKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        var r = a - b;
        output[index] = Sum(r);
    }

    public static void QuaternionMulQuatKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        var r = a * b;
        output[index] = Sum(r);
    }

    public static void QuaternionMulScalarKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, float s)
    {
        var r = a * s;
        output[index] = Sum(r);
    }

    public static void QuaternionDivKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        var r = a / b;
        output[index] = Sum(r);
    }

    public static void QuaternionNegateKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a)
    {
        var r = -a;
        output[index] = Sum(r);
    }

    public static void QuaternionDotKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        output[index] = Quaternion.Dot(a, b);
    }

    public static void QuaternionLengthKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        output[index] = q.Length();
    }

    public static void QuaternionLengthSquaredKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        output[index] = q.LengthSquared();
    }

    public static void QuaternionNormalizeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        output[index] = Sum(Quaternion.Normalize(q));
    }

    public static void QuaternionInverseKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        output[index] = Sum(Quaternion.Inverse(q));
    }

    public static void QuaternionConjugateKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        output[index] = Sum(Quaternion.Conjugate(q));
    }

    public static void QuaternionConcatenateKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b)
    {
        output[index] = Sum(Quaternion.Concatenate(a, b));
    }

    public static void QuaternionCreateFromAxisAngleKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 axis, float angle)
    {
        output[index] = Sum(Quaternion.CreateFromAxisAngle(axis, angle));
    }

    public static void QuaternionCreateFromYawPitchRollKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float yaw, float pitch, float roll)
    {
        output[index] = Sum(Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll));
    }

    public static void QuaternionCreateFromRotationMatrixKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 m)
    {
        output[index] = Sum(Quaternion.CreateFromRotationMatrix(m));
    }

    public static void QuaternionLerpKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b, float t)
    {
        output[index] = Sum(Quaternion.Lerp(a, b, t));
    }

    public static void QuaternionSlerpKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion a, Quaternion b, float t)
    {
        output[index] = Sum(Quaternion.Slerp(a, b, t));
    }

    /// <summary>
    /// Vector3.Transform(Vector3, Quaternion) — was deferred from the
    /// Vector3 surface PR pending Quaternion remap.
    /// </summary>
    public static void Vector3TransformByQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 v, Quaternion q)
    {
        var r = Vector3.Transform(v, q);
        output[index] = r.X + r.Y + r.Z;
    }

    public static void Vector2TransformByQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector2 v, Quaternion q)
    {
        var r = Vector2.Transform(v, q);
        output[index] = r.X + r.Y;
    }

    public static void Vector4TransformVector4ByQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v, Quaternion q)
    {
        var r = Vector4.Transform(v, q);
        output[index] = r.X + r.Y + r.Z + r.W;
    }

    public static void Matrix4x4CreateFromQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Quaternion q)
    {
        var m = Matrix4x4.CreateFromQuaternion(q);
        output[index] = m.M11 + m.M22 + m.M33 + m.M44;
    }

    public static void Matrix4x4CreateFromAxisAngleKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 axis, float angle)
    {
        var m = Matrix4x4.CreateFromAxisAngle(axis, angle);
        output[index] = m.M11 + m.M22 + m.M33 + m.M44;
    }

    public static void Matrix4x4CreateFromYawPitchRollKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float yaw, float pitch, float roll)
    {
        var m = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll);
        output[index] = m.M11 + m.M22 + m.M33 + m.M44;
    }

    public static void Matrix4x4TransformByQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 m, Quaternion q)
    {
        var r = Matrix4x4.Transform(m, q);
        output[index] = r.M11 + r.M22 + r.M33 + r.M44;
    }
}
