// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XMatrix3x2.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Matrix3x2"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XMatrix3x2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(
        ref Matrix3x2 self,
        float m11, float m12,
        float m21, float m22,
        float m31, float m32)
    {
        self.M11 = m11; self.M12 = m12;
        self.M21 = m21; self.M22 = m22;
        self.M31 = m31; self.M32 = m32;
    }

    internal static Matrix3x2 Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Matrix3x2 m = default;
            m.M11 = 1f; m.M22 = 1f;
            return m;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Matrix3x2 a, Matrix3x2 b) =>
        a.M11 == b.M11 && a.M12 == b.M12 &&
        a.M21 == b.M21 && a.M22 == b.M22 &&
        a.M31 == b.M31 && a.M32 == b.M32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Matrix3x2 a, Matrix3x2 b) =>
        !op_Equality(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 op_Addition(Matrix3x2 a, Matrix3x2 b)
    {
        Matrix3x2 m = default;
        m.M11 = a.M11 + b.M11; m.M12 = a.M12 + b.M12;
        m.M21 = a.M21 + b.M21; m.M22 = a.M22 + b.M22;
        m.M31 = a.M31 + b.M31; m.M32 = a.M32 + b.M32;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 op_Subtraction(Matrix3x2 a, Matrix3x2 b)
    {
        Matrix3x2 m = default;
        m.M11 = a.M11 - b.M11; m.M12 = a.M12 - b.M12;
        m.M21 = a.M21 - b.M21; m.M22 = a.M22 - b.M22;
        m.M31 = a.M31 - b.M31; m.M32 = a.M32 - b.M32;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 op_UnaryNegation(Matrix3x2 a)
    {
        Matrix3x2 m = default;
        m.M11 = -a.M11; m.M12 = -a.M12;
        m.M21 = -a.M21; m.M22 = -a.M22;
        m.M31 = -a.M31; m.M32 = -a.M32;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 op_Multiply(Matrix3x2 a, Matrix3x2 b)
    {
        Matrix3x2 m = default;
        m.M11 = a.M11 * b.M11 + a.M12 * b.M21;
        m.M12 = a.M11 * b.M12 + a.M12 * b.M22;
        m.M21 = a.M21 * b.M11 + a.M22 * b.M21;
        m.M22 = a.M21 * b.M12 + a.M22 * b.M22;
        m.M31 = a.M31 * b.M11 + a.M32 * b.M21 + b.M31;
        m.M32 = a.M31 * b.M12 + a.M32 * b.M22 + b.M32;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 op_Multiply(Matrix3x2 a, float s)
    {
        Matrix3x2 m = default;
        m.M11 = a.M11 * s; m.M12 = a.M12 * s;
        m.M21 = a.M21 * s; m.M22 = a.M22 * s;
        m.M31 = a.M31 * s; m.M32 = a.M32 * s;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Add(Matrix3x2 a, Matrix3x2 b) => op_Addition(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Subtract(Matrix3x2 a, Matrix3x2 b) => op_Subtraction(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Negate(Matrix3x2 a) => op_UnaryNegation(a);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Multiply(Matrix3x2 a, Matrix3x2 b) => op_Multiply(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Multiply(Matrix3x2 a, float s) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateTranslation(float x, float y)
    {
        Matrix3x2 m = default;
        m.M11 = 1f; m.M22 = 1f;
        m.M31 = x; m.M32 = y;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateTranslation(Vector2 position) =>
        CreateTranslation(position.X, position.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateScale(float xScale, float yScale)
    {
        Matrix3x2 m = default;
        m.M11 = xScale; m.M22 = yScale;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateScale(float scale) => CreateScale(scale, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateScale(Vector2 scales) =>
        CreateScale(scales.X, scales.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 CreateRotation(float radians)
    {
        float c = XMath.Cos(radians);
        float s = XMath.Sin(radians);
        Matrix3x2 m = default;
        m.M11 = c;  m.M12 = s;
        m.M21 = -s; m.M22 = c;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetDeterminant(ref Matrix3x2 self) =>
        self.M11 * self.M22 - self.M21 * self.M12;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix3x2 Lerp(Matrix3x2 a, Matrix3x2 b, float t)
    {
        Matrix3x2 m = default;
        m.M11 = a.M11 + (b.M11 - a.M11) * t;
        m.M12 = a.M12 + (b.M12 - a.M12) * t;
        m.M21 = a.M21 + (b.M21 - a.M21) * t;
        m.M22 = a.M22 + (b.M22 - a.M22) * t;
        m.M31 = a.M31 + (b.M31 - a.M31) * t;
        m.M32 = a.M32 + (b.M32 - a.M32) * t;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Invert(Matrix3x2 a, out Matrix3x2 result)
    {
        float det = a.M11 * a.M22 - a.M21 * a.M12;
        if (XMath.Abs(det) < 1e-30f)
        {
            result = default;
            return false;
        }
        float invDet = 1f / det;
        result = default;
        result.M11 =  a.M22 * invDet;
        result.M12 = -a.M12 * invDet;
        result.M21 = -a.M21 * invDet;
        result.M22 =  a.M11 * invDet;
        result.M31 = (a.M21 * a.M32 - a.M31 * a.M22) * invDet;
        result.M32 = (a.M31 * a.M12 - a.M11 * a.M32) * invDet;
        return true;
    }
}
