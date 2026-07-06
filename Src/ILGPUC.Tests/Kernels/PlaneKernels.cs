// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PlaneKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels using the <see cref="Plane"/> surface, whose BCL implementation
/// uses hardware intrinsics like the Vector/Matrix types.
/// </summary>
static class PlaneKernels
{
    private static float Sum(in Plane p) =>
        p.Normal.X + p.Normal.Y + p.Normal.Z + p.D;

    public static void PlaneCtorFieldsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        float x, float y, float z, float d)
    {
        var p = new Plane(x, y, z, d);
        output[index] = Sum(p);
    }

    public static void PlaneCtorVector3Kernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 normal, float d)
    {
        var p = new Plane(normal, d);
        output[index] = Sum(p);
    }

    public static void PlaneCtorVector4Kernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector4 v)
    {
        var p = new Plane(v);
        output[index] = Sum(p);
    }

    public static void PlaneCreateFromVerticesKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Vector3 a, Vector3 b, Vector3 c)
    {
        var p = Plane.CreateFromVertices(a, b, c);
        output[index] = Sum(p);
    }

    public static void PlaneDotKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p, Vector4 v)
    {
        output[index] = Plane.Dot(p, v);
    }

    public static void PlaneDotCoordinateKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p, Vector3 v)
    {
        output[index] = Plane.DotCoordinate(p, v);
    }

    public static void PlaneDotNormalKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p, Vector3 v)
    {
        output[index] = Plane.DotNormal(p, v);
    }

    public static void PlaneNormalizeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p)
    {
        output[index] = Sum(Plane.Normalize(p));
    }

    public static void PlaneTransformByMatrixKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p, Matrix4x4 m)
    {
        output[index] = Sum(Plane.Transform(p, m));
    }

    public static void PlaneTransformByQuaternionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Plane p, Quaternion q)
    {
        output[index] = Sum(Plane.Transform(p, q));
    }
}
