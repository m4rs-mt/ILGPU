// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XMatrix4x4.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Matrix4x4"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XMatrix4x4
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(
        ref Matrix4x4 self,
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44)
    {
        self.M11 = m11; self.M12 = m12; self.M13 = m13; self.M14 = m14;
        self.M21 = m21; self.M22 = m22; self.M23 = m23; self.M24 = m24;
        self.M31 = m31; self.M32 = m32; self.M33 = m33; self.M34 = m34;
        self.M41 = m41; self.M42 = m42; self.M43 = m43; self.M44 = m44;
    }

    internal static Matrix4x4 Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Matrix4x4 m = default;
            m.M11 = 1f; m.M22 = 1f; m.M33 = 1f; m.M44 = 1f;
            return m;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Matrix4x4 a, Matrix4x4 b) =>
        a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14 &&
        a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24 &&
        a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34 &&
        a.M41 == b.M41 && a.M42 == b.M42 && a.M43 == b.M43 && a.M44 == b.M44;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Matrix4x4 a, Matrix4x4 b) =>
        !op_Equality(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 op_Addition(Matrix4x4 a, Matrix4x4 b)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11 + b.M11; m.M12 = a.M12 + b.M12;
        m.M13 = a.M13 + b.M13; m.M14 = a.M14 + b.M14;
        m.M21 = a.M21 + b.M21; m.M22 = a.M22 + b.M22;
        m.M23 = a.M23 + b.M23; m.M24 = a.M24 + b.M24;
        m.M31 = a.M31 + b.M31; m.M32 = a.M32 + b.M32;
        m.M33 = a.M33 + b.M33; m.M34 = a.M34 + b.M34;
        m.M41 = a.M41 + b.M41; m.M42 = a.M42 + b.M42;
        m.M43 = a.M43 + b.M43; m.M44 = a.M44 + b.M44;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 op_Subtraction(Matrix4x4 a, Matrix4x4 b)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11 - b.M11; m.M12 = a.M12 - b.M12;
        m.M13 = a.M13 - b.M13; m.M14 = a.M14 - b.M14;
        m.M21 = a.M21 - b.M21; m.M22 = a.M22 - b.M22;
        m.M23 = a.M23 - b.M23; m.M24 = a.M24 - b.M24;
        m.M31 = a.M31 - b.M31; m.M32 = a.M32 - b.M32;
        m.M33 = a.M33 - b.M33; m.M34 = a.M34 - b.M34;
        m.M41 = a.M41 - b.M41; m.M42 = a.M42 - b.M42;
        m.M43 = a.M43 - b.M43; m.M44 = a.M44 - b.M44;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 op_UnaryNegation(Matrix4x4 a)
    {
        Matrix4x4 m = default;
        m.M11 = -a.M11; m.M12 = -a.M12; m.M13 = -a.M13; m.M14 = -a.M14;
        m.M21 = -a.M21; m.M22 = -a.M22; m.M23 = -a.M23; m.M24 = -a.M24;
        m.M31 = -a.M31; m.M32 = -a.M32; m.M33 = -a.M33; m.M34 = -a.M34;
        m.M41 = -a.M41; m.M42 = -a.M42; m.M43 = -a.M43; m.M44 = -a.M44;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 op_Multiply(Matrix4x4 a, Matrix4x4 b)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41;
        m.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42;
        m.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43;
        m.M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44;
        m.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41;
        m.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42;
        m.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43;
        m.M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44;
        m.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41;
        m.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42;
        m.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43;
        m.M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44;
        m.M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41;
        m.M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42;
        m.M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43;
        m.M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 op_Multiply(Matrix4x4 a, float s)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11 * s; m.M12 = a.M12 * s; m.M13 = a.M13 * s; m.M14 = a.M14 * s;
        m.M21 = a.M21 * s; m.M22 = a.M22 * s; m.M23 = a.M23 * s; m.M24 = a.M24 * s;
        m.M31 = a.M31 * s; m.M32 = a.M32 * s; m.M33 = a.M33 * s; m.M34 = a.M34 * s;
        m.M41 = a.M41 * s; m.M42 = a.M42 * s; m.M43 = a.M43 * s; m.M44 = a.M44 * s;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Add(Matrix4x4 a, Matrix4x4 b) => op_Addition(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Subtract(Matrix4x4 a, Matrix4x4 b) => op_Subtraction(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Negate(Matrix4x4 a) => op_UnaryNegation(a);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b) => op_Multiply(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Multiply(Matrix4x4 a, float s) => op_Multiply(a, s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Transpose(Matrix4x4 a)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11; m.M12 = a.M21; m.M13 = a.M31; m.M14 = a.M41;
        m.M21 = a.M12; m.M22 = a.M22; m.M23 = a.M32; m.M24 = a.M42;
        m.M31 = a.M13; m.M32 = a.M23; m.M33 = a.M33; m.M34 = a.M43;
        m.M41 = a.M14; m.M42 = a.M24; m.M43 = a.M34; m.M44 = a.M44;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateTranslation(float x, float y, float z)
    {
        Matrix4x4 m = default;
        m.M11 = 1f; m.M22 = 1f; m.M33 = 1f; m.M44 = 1f;
        m.M41 = x; m.M42 = y; m.M43 = z;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateTranslation(Vector3 position) =>
        CreateTranslation(position.X, position.Y, position.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateScale(float x, float y, float z)
    {
        Matrix4x4 m = default;
        m.M11 = x; m.M22 = y; m.M33 = z; m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateScale(float scale) =>
        CreateScale(scale, scale, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateScale(Vector3 scales) =>
        CreateScale(scales.X, scales.Y, scales.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateRotationX(float radians)
    {
        float c = XMath.Cos(radians);
        float s = XMath.Sin(radians);
        Matrix4x4 m = default;
        m.M11 = 1f;
        m.M22 = c;  m.M23 = s;
        m.M32 = -s; m.M33 = c;
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateRotationY(float radians)
    {
        float c = XMath.Cos(radians);
        float s = XMath.Sin(radians);
        Matrix4x4 m = default;
        m.M11 = c;  m.M13 = -s;
        m.M22 = 1f;
        m.M31 = s;  m.M33 = c;
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateRotationZ(float radians)
    {
        float c = XMath.Cos(radians);
        float s = XMath.Sin(radians);
        Matrix4x4 m = default;
        m.M11 = c;  m.M12 = s;
        m.M21 = -s; m.M22 = c;
        m.M33 = 1f;
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreatePerspectiveFieldOfView(
        float fieldOfView,
        float aspectRatio,
        float nearPlaneDistance,
        float farPlaneDistance)
    {
        float yScale = 1f / XMath.Tan(fieldOfView * 0.5f);
        float xScale = yScale / aspectRatio;
        float negFarRange = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);

        Matrix4x4 m = default;
        m.M11 = xScale;
        m.M22 = yScale;
        m.M33 = negFarRange;
        m.M34 = -1f;
        m.M43 = nearPlaneDistance * negFarRange;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateFromQuaternion(Quaternion q)
    {
        float xx = q.X * q.X;
        float yy = q.Y * q.Y;
        float zz = q.Z * q.Z;
        float xy = q.X * q.Y;
        float wz = q.Z * q.W;
        float xz = q.Z * q.X;
        float wy = q.Y * q.W;
        float yz = q.Y * q.Z;
        float wx = q.X * q.W;

        Matrix4x4 m = default;
        m.M11 = 1f - 2f * (yy + zz);
        m.M12 = 2f * (xy + wz);
        m.M13 = 2f * (xz - wy);
        m.M21 = 2f * (xy - wz);
        m.M22 = 1f - 2f * (zz + xx);
        m.M23 = 2f * (yz + wx);
        m.M31 = 2f * (xz + wy);
        m.M32 = 2f * (yz - wx);
        m.M33 = 1f - 2f * (yy + xx);
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateFromAxisAngle(Vector3 axis, float angle)
    {
        // Direct rotation-matrix formula (Rodrigues), avoids round-tripping
        // through Quaternion.
        float x = axis.X, y = axis.Y, z = axis.Z;
        float sa = XMath.Sin(angle);
        float ca = XMath.Cos(angle);
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;

        Matrix4x4 m = default;
        m.M11 = xx + ca * (1f - xx);
        m.M12 = xy - ca * xy + sa * z;
        m.M13 = xz - ca * xz - sa * y;
        m.M21 = xy - ca * xy - sa * z;
        m.M22 = yy + ca * (1f - yy);
        m.M23 = yz - ca * yz + sa * x;
        m.M31 = xz - ca * xz + sa * y;
        m.M32 = yz - ca * yz - sa * x;
        m.M33 = zz + ca * (1f - zz);
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateFromYawPitchRoll(
        float yaw, float pitch, float roll) =>
        CreateFromQuaternion(
            XQuaternion.CreateFromYawPitchRoll(yaw, pitch, roll));

    /// <summary>
    /// Rotates the rotation/scale portion of <paramref name="value"/> by
    /// the given quaternion. The translation row is preserved.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Transform(Matrix4x4 value, Quaternion rotation)
    {
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

        float q11 = 1f - yy2 - zz2;
        float q21 = xy2 - wz2;
        float q31 = xz2 + wy2;
        float q12 = xy2 + wz2;
        float q22 = 1f - xx2 - zz2;
        float q32 = yz2 - wx2;
        float q13 = xz2 - wy2;
        float q23 = yz2 + wx2;
        float q33 = 1f - xx2 - yy2;

        Matrix4x4 r = default;
        r.M11 = value.M11 * q11 + value.M12 * q21 + value.M13 * q31;
        r.M12 = value.M11 * q12 + value.M12 * q22 + value.M13 * q32;
        r.M13 = value.M11 * q13 + value.M12 * q23 + value.M13 * q33;
        r.M14 = value.M14;
        r.M21 = value.M21 * q11 + value.M22 * q21 + value.M23 * q31;
        r.M22 = value.M21 * q12 + value.M22 * q22 + value.M23 * q32;
        r.M23 = value.M21 * q13 + value.M22 * q23 + value.M23 * q33;
        r.M24 = value.M24;
        r.M31 = value.M31 * q11 + value.M32 * q21 + value.M33 * q31;
        r.M32 = value.M31 * q12 + value.M32 * q22 + value.M33 * q32;
        r.M33 = value.M31 * q13 + value.M32 * q23 + value.M33 * q33;
        r.M34 = value.M34;
        r.M41 = value.M41 * q11 + value.M42 * q21 + value.M43 * q31;
        r.M42 = value.M41 * q12 + value.M42 * q22 + value.M43 * q32;
        r.M43 = value.M41 * q13 + value.M42 * q23 + value.M43 * q33;
        r.M44 = value.M44;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 CreateLookAt(
        Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
    {
        Vector3 zaxis = XVector3.Normalize(
            XVector3.op_Subtraction(cameraPosition, cameraTarget));
        Vector3 xaxis = XVector3.Normalize(XVector3.Cross(cameraUpVector, zaxis));
        Vector3 yaxis = XVector3.Cross(zaxis, xaxis);

        Matrix4x4 m = default;
        m.M11 = xaxis.X; m.M12 = yaxis.X; m.M13 = zaxis.X;
        m.M21 = xaxis.Y; m.M22 = yaxis.Y; m.M23 = zaxis.Y;
        m.M31 = xaxis.Z; m.M32 = yaxis.Z; m.M33 = zaxis.Z;
        m.M41 = -XVector3.Dot(xaxis, cameraPosition);
        m.M42 = -XVector3.Dot(yaxis, cameraPosition);
        m.M43 = -XVector3.Dot(zaxis, cameraPosition);
        m.M44 = 1f;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetDeterminant(ref Matrix4x4 self)
    {
        float a = self.M11, b = self.M12, c = self.M13, d = self.M14;
        float e = self.M21, f = self.M22, g = self.M23, h = self.M24;
        float i = self.M31, j = self.M32, k = self.M33, l = self.M34;
        float m = self.M41, n = self.M42, o = self.M43, p = self.M44;

        float kp_lo = k * p - l * o;
        float jp_ln = j * p - l * n;
        float jo_kn = j * o - k * n;
        float ip_lm = i * p - l * m;
        float io_km = i * o - k * m;
        float in_jm = i * n - j * m;

        return a * (f * kp_lo - g * jp_ln + h * jo_kn)
             - b * (e * kp_lo - g * ip_lm + h * io_km)
             + c * (e * jp_ln - f * ip_lm + h * in_jm)
             - d * (e * jo_kn - f * io_km + g * in_jm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Lerp(Matrix4x4 a, Matrix4x4 b, float t)
    {
        Matrix4x4 m = default;
        m.M11 = a.M11 + (b.M11 - a.M11) * t; m.M12 = a.M12 + (b.M12 - a.M12) * t;
        m.M13 = a.M13 + (b.M13 - a.M13) * t; m.M14 = a.M14 + (b.M14 - a.M14) * t;
        m.M21 = a.M21 + (b.M21 - a.M21) * t; m.M22 = a.M22 + (b.M22 - a.M22) * t;
        m.M23 = a.M23 + (b.M23 - a.M23) * t; m.M24 = a.M24 + (b.M24 - a.M24) * t;
        m.M31 = a.M31 + (b.M31 - a.M31) * t; m.M32 = a.M32 + (b.M32 - a.M32) * t;
        m.M33 = a.M33 + (b.M33 - a.M33) * t; m.M34 = a.M34 + (b.M34 - a.M34) * t;
        m.M41 = a.M41 + (b.M41 - a.M41) * t; m.M42 = a.M42 + (b.M42 - a.M42) * t;
        m.M43 = a.M43 + (b.M43 - a.M43) * t; m.M44 = a.M44 + (b.M44 - a.M44) * t;
        return m;
    }

    /// <summary>
    /// Inverts a 4×4 matrix. Algorithm transcribed from the .NET runtime
    /// (MIT-licensed) using the standard cofactor expansion — no
    /// <c>System.Runtime.Intrinsics</c> calls.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Invert(Matrix4x4 mat, out Matrix4x4 result)
    {
        float a = mat.M11, b = mat.M12, c = mat.M13, d = mat.M14;
        float e = mat.M21, f = mat.M22, g = mat.M23, h = mat.M24;
        float i = mat.M31, j = mat.M32, k = mat.M33, l = mat.M34;
        float m = mat.M41, n = mat.M42, o = mat.M43, p = mat.M44;

        float kp_lo = k * p - l * o;
        float jp_ln = j * p - l * n;
        float jo_kn = j * o - k * n;
        float ip_lm = i * p - l * m;
        float io_km = i * o - k * m;
        float in_jm = i * n - j * m;

        float a11 =  (f * kp_lo - g * jp_ln + h * jo_kn);
        float a12 = -(e * kp_lo - g * ip_lm + h * io_km);
        float a13 =  (e * jp_ln - f * ip_lm + h * in_jm);
        float a14 = -(e * jo_kn - f * io_km + g * in_jm);

        float det = a * a11 + b * a12 + c * a13 + d * a14;
        if (XMath.Abs(det) < 1e-30f)
        {
            result = default;
            return false;
        }

        float invDet = 1f / det;
        result = default;
        result.M11 = a11 * invDet;
        result.M21 = a12 * invDet;
        result.M31 = a13 * invDet;
        result.M41 = a14 * invDet;

        result.M12 = -(b * kp_lo - c * jp_ln + d * jo_kn) * invDet;
        result.M22 =  (a * kp_lo - c * ip_lm + d * io_km) * invDet;
        result.M32 = -(a * jp_ln - b * ip_lm + d * in_jm) * invDet;
        result.M42 =  (a * jo_kn - b * io_km + c * in_jm) * invDet;

        float gp_ho = g * p - h * o;
        float fp_hn = f * p - h * n;
        float fo_gn = f * o - g * n;
        float ep_hm = e * p - h * m;
        float eo_gm = e * o - g * m;
        float en_fm = e * n - f * m;

        result.M13 =  (b * gp_ho - c * fp_hn + d * fo_gn) * invDet;
        result.M23 = -(a * gp_ho - c * ep_hm + d * eo_gm) * invDet;
        result.M33 =  (a * fp_hn - b * ep_hm + d * en_fm) * invDet;
        result.M43 = -(a * fo_gn - b * eo_gm + c * en_fm) * invDet;

        float gl_hk = g * l - h * k;
        float fl_hj = f * l - h * j;
        float fk_gj = f * k - g * j;
        float el_hi = e * l - h * i;
        float ek_gi = e * k - g * i;
        float ej_fi = e * j - f * i;

        result.M14 = -(b * gl_hk - c * fl_hj + d * fk_gj) * invDet;
        result.M24 =  (a * gl_hk - c * el_hi + d * ek_gi) * invDet;
        result.M34 = -(a * fl_hj - b * el_hi + d * ej_fi) * invDet;
        result.M44 =  (a * fk_gj - b * ek_gi + c * ej_fi) * invDet;

        return true;
    }

    /// <summary>
    /// Decomposes an affine matrix into translation, rotation, and scale
    /// components. Mirrors the BCL <see cref="Matrix4x4.Decompose"/>
    /// closed-form algorithm: row-magnitude scale extraction with
    /// determinant-sign disambiguation, then Shepperd's method for the
    /// rotation quaternion (via <see cref="XQuaternion.CreateFromRotationMatrix"/>).
    /// Assumes the upper-left 3×3 columns are orthogonal up to scaling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Decompose(
        Matrix4x4 mat,
        out Vector3 scale,
        out Quaternion rotation,
        out Vector3 translation)
    {
        scale = default;
        rotation = default;
        translation = default;

        translation.X = mat.M41;
        translation.Y = mat.M42;
        translation.Z = mat.M43;

        float sx = XMath.Sqrt(
            mat.M11 * mat.M11 + mat.M12 * mat.M12 + mat.M13 * mat.M13);
        float sy = XMath.Sqrt(
            mat.M21 * mat.M21 + mat.M22 * mat.M22 + mat.M23 * mat.M23);
        float sz = XMath.Sqrt(
            mat.M31 * mat.M31 + mat.M32 * mat.M32 + mat.M33 * mat.M33);

        if (sx < 1e-30f || sy < 1e-30f || sz < 1e-30f)
            return false;

        // Sign-disambiguate via the upper-left 3×3 determinant: a negative
        // determinant means the matrix encodes a reflection on top of the
        // rotation. Conventionally, fold that into scale.X. Multiply by a
        // ±1 mask rather than branching on the sign so the IR phi for sx
        // stays a uniform value across both flow paths (otherwise the CPU
        // backend ends up with sx undefined on the positive-det path).
        float det3 =
            mat.M11 * (mat.M22 * mat.M33 - mat.M23 * mat.M32) -
            mat.M12 * (mat.M21 * mat.M33 - mat.M23 * mat.M31) +
            mat.M13 * (mat.M21 * mat.M32 - mat.M22 * mat.M31);
        float sxSign = det3 < 0f ? -1f : 1f;
        sx = sx * sxSign;

        scale.X = sx;
        scale.Y = sy;
        scale.Z = sz;

        float invSx = 1f / sx;
        float invSy = 1f / sy;
        float invSz = 1f / sz;

        Matrix4x4 r = default;
        r.M11 = mat.M11 * invSx; r.M12 = mat.M12 * invSx; r.M13 = mat.M13 * invSx;
        r.M21 = mat.M21 * invSy; r.M22 = mat.M22 * invSy; r.M23 = mat.M23 * invSy;
        r.M31 = mat.M31 * invSz; r.M32 = mat.M32 * invSz; r.M33 = mat.M33 * invSz;
        r.M44 = 1f;

        rotation = XQuaternion.CreateFromRotationMatrix(r);
        return true;
    }
}
