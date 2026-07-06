// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XVector2.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Vector2"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XVector2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector2 self, float x, float y)
    {
        self.X = x; self.Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector2 self, float value)
    {
        self.X = value; self.Y = value;
    }

    internal static Vector2 Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector2 v = default; return v; }
    }

    internal static Vector2 One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector2 v = default; v.X = 1f; v.Y = 1f; return v; }
    }

    internal static Vector2 UnitX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector2 v = default; v.X = 1f; return v; }
    }

    internal static Vector2 UnitY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector2 v = default; v.Y = 1f; return v; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Length(ref Vector2 self) =>
        XMath.Sqrt(self.X * self.X + self.Y * self.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float LengthSquared(ref Vector2 self) =>
        self.X * self.X + self.Y * self.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Vector2 a, Vector2 b) =>
        a.X == b.X && a.Y == b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Vector2 a, Vector2 b) =>
        a.X != b.X || a.Y != b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Addition(Vector2 a, Vector2 b)
    { Vector2 r = default; r.X = a.X + b.X; r.Y = a.Y + b.Y; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Subtraction(Vector2 a, Vector2 b)
    { Vector2 r = default; r.X = a.X - b.X; r.Y = a.Y - b.Y; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Multiply(Vector2 a, Vector2 b)
    { Vector2 r = default; r.X = a.X * b.X; r.Y = a.Y * b.Y; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Multiply(Vector2 a, float s)
    { Vector2 r = default; r.X = a.X * s; r.Y = a.Y * s; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Multiply(float s, Vector2 a) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Division(Vector2 a, Vector2 b)
    { Vector2 r = default; r.X = a.X / b.X; r.Y = a.Y / b.Y; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_Division(Vector2 a, float s)
    { Vector2 r = default; r.X = a.X / s; r.Y = a.Y / s; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 op_UnaryNegation(Vector2 a)
    { Vector2 r = default; r.X = -a.X; r.Y = -a.Y; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Add(Vector2 a, Vector2 b) => op_Addition(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Subtract(Vector2 a, Vector2 b) => op_Subtraction(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Multiply(Vector2 a, Vector2 b) => op_Multiply(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Multiply(Vector2 a, float s) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Multiply(float s, Vector2 a) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Divide(Vector2 a, Vector2 b) => op_Division(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Divide(Vector2 a, float s) => op_Division(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Negate(Vector2 a) => op_UnaryNegation(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Normalize(Vector2 v)
    {
        float invLen = 1f / XMath.Sqrt(v.X * v.X + v.Y * v.Y);
        Vector2 r = default; r.X = v.X * invLen; r.Y = v.Y * invLen; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Distance(Vector2 a, Vector2 b)
    { float dx = a.X - b.X, dy = a.Y - b.Y; return XMath.Sqrt(dx * dx + dy * dy); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DistanceSquared(Vector2 a, Vector2 b)
    { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Abs(Vector2 v)
    { Vector2 r = default; r.X = XMath.Abs(v.X); r.Y = XMath.Abs(v.Y); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 SquareRoot(Vector2 v)
    { Vector2 r = default; r.X = XMath.Sqrt(v.X); r.Y = XMath.Sqrt(v.Y); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Min(Vector2 a, Vector2 b)
    {
        Vector2 r = default;
        r.X = a.X < b.X ? a.X : b.X;
        r.Y = a.Y < b.Y ? a.Y : b.Y;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Max(Vector2 a, Vector2 b)
    {
        Vector2 r = default;
        r.X = a.X > b.X ? a.X : b.X;
        r.Y = a.Y > b.Y ? a.Y : b.Y;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Clamp(Vector2 v, Vector2 lo, Vector2 hi) =>
        Min(Max(v, lo), hi);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        Vector2 r = default;
        r.X = a.X + (b.X - a.X) * t;
        r.Y = a.Y + (b.Y - a.Y) * t;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Reflect(Vector2 v, Vector2 n)
    {
        float d2 = 2f * (v.X * n.X + v.Y * n.Y);
        Vector2 r = default;
        r.X = v.X - n.X * d2;
        r.Y = v.Y - n.Y * d2;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Transform(Vector2 v, Matrix3x2 m)
    {
        Vector2 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + m.M31;
        r.Y = v.X * m.M12 + v.Y * m.M22 + m.M32;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Transform(Vector2 v, Matrix4x4 m)
    {
        Vector2 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + m.M41;
        r.Y = v.X * m.M12 + v.Y * m.M22 + m.M42;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 TransformNormal(Vector2 v, Matrix3x2 m)
    {
        Vector2 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21;
        r.Y = v.X * m.M12 + v.Y * m.M22;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 TransformNormal(Vector2 v, Matrix4x4 m)
    {
        Vector2 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21;
        r.Y = v.X * m.M12 + v.Y * m.M22;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Transform(Vector2 v, Quaternion q)
    {
        float x2 = q.X + q.X;
        float y2 = q.Y + q.Y;
        float z2 = q.Z + q.Z;
        float wz2 = q.W * z2;
        float xx2 = q.X * x2, xy2 = q.X * y2;
        float yy2 = q.Y * y2;
        float zz2 = q.Z * z2;

        Vector2 r = default;
        r.X = v.X * (1f - yy2 - zz2) + v.Y * (xy2 - wz2);
        r.Y = v.X * (xy2 + wz2) + v.Y * (1f - xx2 - zz2);
        return r;
    }
}
