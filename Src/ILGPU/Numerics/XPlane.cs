// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XPlane.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Plane"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XPlane
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(
        ref Plane self, float x, float y, float z, float d)
    {
        self.Normal = new Vector3(x, y, z);
        self.D = d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Plane self, Vector3 normal, float d)
    {
        self.Normal = normal;
        self.D = d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Plane self, Vector4 v)
    {
        self.Normal = new Vector3(v.X, v.Y, v.Z);
        self.D = v.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Plane a, Plane b) =>
        a.Normal.X == b.Normal.X &&
        a.Normal.Y == b.Normal.Y &&
        a.Normal.Z == b.Normal.Z &&
        a.D == b.D;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Plane a, Plane b) => !op_Equality(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Plane CreateFromVertices(Vector3 a, Vector3 b, Vector3 c)
    {
        // Edge vectors
        float ax = b.X - a.X, ay = b.Y - a.Y, az = b.Z - a.Z;
        float bx = c.X - a.X, by = c.Y - a.Y, bz = c.Z - a.Z;

        // Cross product (normal)
        float nx = ay * bz - az * by;
        float ny = az * bx - ax * bz;
        float nz = ax * by - ay * bx;

        // Normalize
        float invLen = 1f / XMath.Sqrt(nx * nx + ny * ny + nz * nz);
        nx *= invLen; ny *= invLen; nz *= invLen;

        Plane p = default;
        p.Normal = new Vector3(nx, ny, nz);
        p.D = -(nx * a.X + ny * a.Y + nz * a.Z);
        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Dot(Plane plane, Vector4 value) =>
        plane.Normal.X * value.X +
        plane.Normal.Y * value.Y +
        plane.Normal.Z * value.Z +
        plane.D * value.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DotCoordinate(Plane plane, Vector3 value) =>
        plane.Normal.X * value.X +
        plane.Normal.Y * value.Y +
        plane.Normal.Z * value.Z +
        plane.D;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DotNormal(Plane plane, Vector3 value) =>
        plane.Normal.X * value.X +
        plane.Normal.Y * value.Y +
        plane.Normal.Z * value.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Plane Normalize(Plane plane)
    {
        float lenSquared =
            plane.Normal.X * plane.Normal.X +
            plane.Normal.Y * plane.Normal.Y +
            plane.Normal.Z * plane.Normal.Z;
        // BCL fast-path: if already unit-length within eps, return as-is.
        // We skip the fast-path on GPU — branch is more expensive than the
        // sqrt for short vectors and rsqrt is a single instruction on most
        // GPUs. Always normalize.
        float invLen = 1f / XMath.Sqrt(lenSquared);
        Plane r = default;
        r.Normal = new Vector3(
            plane.Normal.X * invLen,
            plane.Normal.Y * invLen,
            plane.Normal.Z * invLen);
        r.D = plane.D * invLen;
        return r;
    }

    /// <summary>
    /// Transforms a plane by a matrix using the inverse-transpose of
    /// the matrix applied to the (Normal, D) 4-vector. Matrix must be
    /// invertible; otherwise behaviour is undefined (returns the
    /// formula's result with the singular inverse).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Plane Transform(Plane plane, Matrix4x4 matrix)
    {
        XMatrix4x4.Invert(matrix, out var m);

        float x = plane.Normal.X, y = plane.Normal.Y, z = plane.Normal.Z;
        float w = plane.D;

        Plane r = default;
        r.Normal = new Vector3(
            x * m.M11 + y * m.M12 + z * m.M13 + w * m.M14,
            x * m.M21 + y * m.M22 + z * m.M23 + w * m.M24,
            x * m.M31 + y * m.M32 + z * m.M33 + w * m.M34);
        r.D = x * m.M41 + y * m.M42 + z * m.M43 + w * m.M44;
        return r;
    }

    /// <summary>
    /// Transforms a plane by a rotation. Pure rotations preserve the
    /// distance D; only the normal is rotated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Plane Transform(Plane plane, Quaternion rotation)
    {
        // Build 3×3 rotation matrix from quaternion.
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float z2 = rotation.Z + rotation.Z;
        float wx2 = rotation.W * x2;
        float wy2 = rotation.W * y2;
        float wz2 = rotation.W * z2;
        float xx2 = rotation.X * x2;
        float xy2 = rotation.X * y2;
        float xz2 = rotation.X * z2;
        float yy2 = rotation.Y * y2;
        float yz2 = rotation.Y * z2;
        float zz2 = rotation.Z * z2;

        float m11 = 1f - yy2 - zz2;
        float m21 = xy2 - wz2;
        float m31 = xz2 + wy2;
        float m12 = xy2 + wz2;
        float m22 = 1f - xx2 - zz2;
        float m32 = yz2 - wx2;
        float m13 = xz2 - wy2;
        float m23 = yz2 + wx2;
        float m33 = 1f - xx2 - yy2;

        float x = plane.Normal.X, y = plane.Normal.Y, z = plane.Normal.Z;

        Plane r = default;
        r.Normal = new Vector3(
            x * m11 + y * m21 + z * m31,
            x * m12 + y * m22 + z * m32,
            x * m13 + y * m23 + z * m33);
        r.D = plane.D;
        return r;
    }
}
