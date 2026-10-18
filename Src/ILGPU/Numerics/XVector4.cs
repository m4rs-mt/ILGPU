// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XVector4.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Vector4"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XVector4
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector4 self, float x, float y, float z, float w)
    {
        self.X = x; self.Y = y; self.Z = z; self.W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector4 self, float value)
    {
        self.X = value; self.Y = value; self.Z = value; self.W = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector4 self, Vector2 xy, float z, float w)
    {
        self.X = xy.X; self.Y = xy.Y; self.Z = z; self.W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector4 self, Vector3 xyz, float w)
    {
        self.X = xyz.X; self.Y = xyz.Y; self.Z = xyz.Z; self.W = w;
    }

    internal static Vector4 Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector4 v = default; return v; }
    }

    internal static Vector4 One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Vector4 v = default;
            v.X = 1f; v.Y = 1f; v.Z = 1f; v.W = 1f;
            return v;
        }
    }

    internal static Vector4 UnitX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector4 v = default; v.X = 1f; return v; }
    }

    internal static Vector4 UnitY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector4 v = default; v.Y = 1f; return v; }
    }

    internal static Vector4 UnitZ
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector4 v = default; v.Z = 1f; return v; }
    }

    internal static Vector4 UnitW
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector4 v = default; v.W = 1f; return v; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Length(ref Vector4 self) =>
        XMath.Sqrt(self.X * self.X + self.Y * self.Y + self.Z * self.Z + self.W * self.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float LengthSquared(ref Vector4 self) =>
        self.X * self.X + self.Y * self.Y + self.Z * self.Z + self.W * self.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Vector4 a, Vector4 b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Vector4 a, Vector4 b) =>
        a.X != b.X || a.Y != b.Y || a.Z != b.Z || a.W != b.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Addition(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X + b.X; r.Y = a.Y + b.Y; r.Z = a.Z + b.Z; r.W = a.W + b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Subtraction(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X - b.X; r.Y = a.Y - b.Y; r.Z = a.Z - b.Z; r.W = a.W - b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Multiply(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X * b.X; r.Y = a.Y * b.Y; r.Z = a.Z * b.Z; r.W = a.W * b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Multiply(Vector4 a, float s)
    {
        Vector4 r = default;
        r.X = a.X * s; r.Y = a.Y * s; r.Z = a.Z * s; r.W = a.W * s;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Multiply(float s, Vector4 a) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Division(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X / b.X; r.Y = a.Y / b.Y; r.Z = a.Z / b.Z; r.W = a.W / b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_Division(Vector4 a, float s)
    {
        Vector4 r = default;
        r.X = a.X / s; r.Y = a.Y / s; r.Z = a.Z / s; r.W = a.W / s;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 op_UnaryNegation(Vector4 a)
    {
        Vector4 r = default;
        r.X = -a.X; r.Y = -a.Y; r.Z = -a.Z; r.W = -a.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Add(Vector4 a, Vector4 b) => op_Addition(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Subtract(Vector4 a, Vector4 b) => op_Subtraction(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Multiply(Vector4 a, Vector4 b) => op_Multiply(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Multiply(Vector4 a, float s) => op_Multiply(a, s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Multiply(float s, Vector4 a) => op_Multiply(a, s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Divide(Vector4 a, Vector4 b) => op_Division(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Divide(Vector4 a, float s) => op_Division(a, s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Negate(Vector4 a) => op_UnaryNegation(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Dot(Vector4 a, Vector4 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Normalize(Vector4 v)
    {
        float invLen = 1f / XMath.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z + v.W * v.W);
        Vector4 r = default;
        r.X = v.X * invLen; r.Y = v.Y * invLen; r.Z = v.Z * invLen; r.W = v.W * invLen;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Distance(Vector4 a, Vector4 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z, dw = a.W - b.W;
        return XMath.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DistanceSquared(Vector4 a, Vector4 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z, dw = a.W - b.W;
        return dx * dx + dy * dy + dz * dz + dw * dw;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Abs(Vector4 v)
    {
        Vector4 r = default;
        r.X = XMath.Abs(v.X); r.Y = XMath.Abs(v.Y);
        r.Z = XMath.Abs(v.Z); r.W = XMath.Abs(v.W);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 SquareRoot(Vector4 v)
    {
        Vector4 r = default;
        r.X = XMath.Sqrt(v.X); r.Y = XMath.Sqrt(v.Y);
        r.Z = XMath.Sqrt(v.Z); r.W = XMath.Sqrt(v.W);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Min(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X < b.X ? a.X : b.X;
        r.Y = a.Y < b.Y ? a.Y : b.Y;
        r.Z = a.Z < b.Z ? a.Z : b.Z;
        r.W = a.W < b.W ? a.W : b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Max(Vector4 a, Vector4 b)
    {
        Vector4 r = default;
        r.X = a.X > b.X ? a.X : b.X;
        r.Y = a.Y > b.Y ? a.Y : b.Y;
        r.Z = a.Z > b.Z ? a.Z : b.Z;
        r.W = a.W > b.W ? a.W : b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Clamp(Vector4 v, Vector4 lo, Vector4 hi) =>
        Min(Max(v, lo), hi);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        Vector4 r = default;
        r.X = a.X + (b.X - a.X) * t;
        r.Y = a.Y + (b.Y - a.Y) * t;
        r.Z = a.Z + (b.Z - a.Z) * t;
        r.W = a.W + (b.W - a.W) * t;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector4 v, Matrix4x4 m)
    {
        Vector4 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + v.W * m.M41;
        r.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + v.W * m.M42;
        r.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + v.W * m.M43;
        r.W = v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + v.W * m.M44;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector2 v, Matrix4x4 m)
    {
        Vector4 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + m.M41;
        r.Y = v.X * m.M12 + v.Y * m.M22 + m.M42;
        r.Z = v.X * m.M13 + v.Y * m.M23 + m.M43;
        r.W = v.X * m.M14 + v.Y * m.M24 + m.M44;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector3 v, Matrix4x4 m)
    {
        Vector4 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41;
        r.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42;
        r.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43;
        r.W = v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + m.M44;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector4 v, Quaternion q)
    {
        float x2 = q.X + q.X;
        float y2 = q.Y + q.Y;
        float z2 = q.Z + q.Z;
        float wx2 = q.W * x2, wy2 = q.W * y2, wz2 = q.W * z2;
        float xx2 = q.X * x2, xy2 = q.X * y2, xz2 = q.X * z2;
        float yy2 = q.Y * y2, yz2 = q.Y * z2;
        float zz2 = q.Z * z2;

        Vector4 r = default;
        r.X = v.X * (1f - yy2 - zz2) + v.Y * (xy2 - wz2) + v.Z * (xz2 + wy2);
        r.Y = v.X * (xy2 + wz2) + v.Y * (1f - xx2 - zz2) + v.Z * (yz2 - wx2);
        r.Z = v.X * (xz2 - wy2) + v.Y * (yz2 + wx2) + v.Z * (1f - xx2 - yy2);
        r.W = v.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector3 v, Quaternion q)
    {
        float x2 = q.X + q.X;
        float y2 = q.Y + q.Y;
        float z2 = q.Z + q.Z;
        float wx2 = q.W * x2, wy2 = q.W * y2, wz2 = q.W * z2;
        float xx2 = q.X * x2, xy2 = q.X * y2, xz2 = q.X * z2;
        float yy2 = q.Y * y2, yz2 = q.Y * z2;
        float zz2 = q.Z * z2;

        Vector4 r = default;
        r.X = v.X * (1f - yy2 - zz2) + v.Y * (xy2 - wz2) + v.Z * (xz2 + wy2);
        r.Y = v.X * (xy2 + wz2) + v.Y * (1f - xx2 - zz2) + v.Z * (yz2 - wx2);
        r.Z = v.X * (xz2 - wy2) + v.Y * (yz2 + wx2) + v.Z * (1f - xx2 - yy2);
        r.W = 1f;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector2 v, Quaternion q)
    {
        float x2 = q.X + q.X;
        float y2 = q.Y + q.Y;
        float z2 = q.Z + q.Z;
        float wz2 = q.W * z2;
        float xx2 = q.X * x2, xy2 = q.X * y2, xz2 = q.X * z2;
        float yy2 = q.Y * y2, yz2 = q.Y * z2;
        float zz2 = q.Z * z2;
        // Note: vector has z=0; some terms drop out vs the Vector3 case.
        float wx2 = q.W * x2, wy2 = q.W * y2;

        Vector4 r = default;
        r.X = v.X * (1f - yy2 - zz2) + v.Y * (xy2 - wz2);
        r.Y = v.X * (xy2 + wz2) + v.Y * (1f - xx2 - zz2);
        r.Z = v.X * (xz2 - wy2) + v.Y * (yz2 + wx2);
        r.W = 1f;
        return r;
    }
}
