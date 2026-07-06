// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XVector3.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Vector3"/> entry points.
/// The .NET 10 BCL fans out into <c>System.Runtime.Intrinsics.Vector{64,128}&lt;T&gt;</c>
/// JIT-replacement stubs that ILGPU's inliner can't consume. Targets here
/// operate per-component and never reach <c>System.Runtime.Intrinsics</c>.
/// Wired in via <c>ILGPUC/Static/Intrinsics/Numerics.xml</c>.
/// </summary>
internal static class XVector3
{
    // -- Constructors -------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector3 self, float x, float y, float z)
    {
        self.X = x;
        self.Y = y;
        self.Z = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector3 self, float value)
    {
        self.X = value;
        self.Y = value;
        self.Z = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(ref Vector3 self, Vector2 xy, float z)
    {
        self.X = xy.X;
        self.Y = xy.Y;
        self.Z = z;
    }

    // -- Properties ---------------------------------------------------------

    internal static Vector3 Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector3 v = default; return v; }
    }

    internal static Vector3 One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Vector3 v = default;
            v.X = 1f; v.Y = 1f; v.Z = 1f;
            return v;
        }
    }

    internal static Vector3 UnitX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector3 v = default; v.X = 1f; return v; }
    }

    internal static Vector3 UnitY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector3 v = default; v.Y = 1f; return v; }
    }

    internal static Vector3 UnitZ
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Vector3 v = default; v.Z = 1f; return v; }
    }

    // -- Instance methods (by ref) -----------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Length(ref Vector3 self) =>
        XMath.Sqrt(self.X * self.X + self.Y * self.Y + self.Z * self.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float LengthSquared(ref Vector3 self) =>
        self.X * self.X + self.Y * self.Y + self.Z * self.Z;

    // -- Operators ----------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Vector3 a, Vector3 b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Vector3 a, Vector3 b) =>
        a.X != b.X || a.Y != b.Y || a.Z != b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Addition(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X + b.X; r.Y = a.Y + b.Y; r.Z = a.Z + b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Subtraction(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X - b.X; r.Y = a.Y - b.Y; r.Z = a.Z - b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Multiply(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X * b.X; r.Y = a.Y * b.Y; r.Z = a.Z * b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Multiply(Vector3 a, float s)
    {
        Vector3 r = default;
        r.X = a.X * s; r.Y = a.Y * s; r.Z = a.Z * s;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Multiply(float s, Vector3 a) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Division(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X / b.X; r.Y = a.Y / b.Y; r.Z = a.Z / b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_Division(Vector3 a, float s)
    {
        Vector3 r = default;
        r.X = a.X / s; r.Y = a.Y / s; r.Z = a.Z / s;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 op_UnaryNegation(Vector3 a)
    {
        Vector3 r = default;
        r.X = -a.X; r.Y = -a.Y; r.Z = -a.Z;
        return r;
    }

    // -- Static math API mirroring System.Numerics.Vector3 ------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Add(Vector3 a, Vector3 b) => op_Addition(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Subtract(Vector3 a, Vector3 b) => op_Subtraction(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Multiply(Vector3 a, Vector3 b) => op_Multiply(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Multiply(Vector3 a, float s) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Multiply(float s, Vector3 a) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Divide(Vector3 a, Vector3 b) => op_Division(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Divide(Vector3 a, float s) => op_Division(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Negate(Vector3 a) => op_UnaryNegation(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Dot(Vector3 a, Vector3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Cross(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.Y * b.Z - a.Z * b.Y;
        r.Y = a.Z * b.X - a.X * b.Z;
        r.Z = a.X * b.Y - a.Y * b.X;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Normalize(Vector3 v)
    {
        float invLen = 1f / XMath.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        Vector3 r = default;
        r.X = v.X * invLen; r.Y = v.Y * invLen; r.Z = v.Z * invLen;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return XMath.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Abs(Vector3 v)
    {
        Vector3 r = default;
        r.X = XMath.Abs(v.X); r.Y = XMath.Abs(v.Y); r.Z = XMath.Abs(v.Z);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Min(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X < b.X ? a.X : b.X;
        r.Y = a.Y < b.Y ? a.Y : b.Y;
        r.Z = a.Z < b.Z ? a.Z : b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Max(Vector3 a, Vector3 b)
    {
        Vector3 r = default;
        r.X = a.X > b.X ? a.X : b.X;
        r.Y = a.Y > b.Y ? a.Y : b.Y;
        r.Z = a.Z > b.Z ? a.Z : b.Z;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Clamp(Vector3 v, Vector3 lo, Vector3 hi) =>
        Min(Max(v, lo), hi);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        Vector3 r = default;
        r.X = a.X + (b.X - a.X) * t;
        r.Y = a.Y + (b.Y - a.Y) * t;
        r.Z = a.Z + (b.Z - a.Z) * t;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Reflect(Vector3 v, Vector3 n)
    {
        float d2 = 2f * (v.X * n.X + v.Y * n.Y + v.Z * n.Z);
        Vector3 r = default;
        r.X = v.X - n.X * d2;
        r.Y = v.Y - n.Y * d2;
        r.Z = v.Z - n.Z * d2;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 SquareRoot(Vector3 v)
    {
        Vector3 r = default;
        r.X = XMath.Sqrt(v.X); r.Y = XMath.Sqrt(v.Y); r.Z = XMath.Sqrt(v.Z);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Transform(Vector3 v, Matrix4x4 m)
    {
        Vector3 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41;
        r.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42;
        r.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 TransformNormal(Vector3 v, Matrix4x4 m)
    {
        Vector3 r = default;
        r.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31;
        r.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32;
        r.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33;
        return r;
    }

    /// <summary>
    /// Rotates a vector by a quaternion (the rotation expressed by q).
    /// Equivalent to expanding q ⊗ (v,0) ⊗ q⁻¹ into a 3×3 multiply.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Transform(Vector3 v, Quaternion q)
    {
        float x2 = q.X + q.X;
        float y2 = q.Y + q.Y;
        float z2 = q.Z + q.Z;
        float wx2 = q.W * x2, wy2 = q.W * y2, wz2 = q.W * z2;
        float xx2 = q.X * x2, xy2 = q.X * y2, xz2 = q.X * z2;
        float yy2 = q.Y * y2, yz2 = q.Y * z2;
        float zz2 = q.Z * z2;

        Vector3 r = default;
        r.X = v.X * (1f - yy2 - zz2) + v.Y * (xy2 - wz2) + v.Z * (xz2 + wy2);
        r.Y = v.X * (xy2 + wz2) + v.Y * (1f - xx2 - zz2) + v.Z * (yz2 - wx2);
        r.Z = v.X * (xz2 - wy2) + v.Y * (yz2 + wx2) + v.Z * (1f - xx2 - yy2);
        return r;
    }
}
