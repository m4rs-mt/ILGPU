// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XQuaternion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Quaternion"/> entry points.
/// See <see cref="XVector3"/> for the rationale.
/// </summary>
internal static class XQuaternion
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(
        ref Quaternion self, float x, float y, float z, float w)
    {
        self.X = x; self.Y = y; self.Z = z; self.W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Construct(
        ref Quaternion self, Vector3 vectorPart, float scalarPart)
    {
        self.X = vectorPart.X;
        self.Y = vectorPart.Y;
        self.Z = vectorPart.Z;
        self.W = scalarPart;
    }

    internal static Quaternion Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Quaternion q = default;
            q.W = 1f;
            return q;
        }
    }

    internal static Quaternion Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { Quaternion q = default; return q; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsIdentity(ref Quaternion self) =>
        self.X == 0f && self.Y == 0f && self.Z == 0f && self.W == 1f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Length(ref Quaternion self) =>
        XMath.Sqrt(
            self.X * self.X + self.Y * self.Y +
            self.Z * self.Z + self.W * self.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float LengthSquared(ref Quaternion self) =>
        self.X * self.X + self.Y * self.Y +
        self.Z * self.Z + self.W * self.W;

    // -- Operators ----------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Equality(Quaternion a, Quaternion b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool op_Inequality(Quaternion a, Quaternion b) =>
        !op_Equality(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_Addition(Quaternion a, Quaternion b)
    {
        Quaternion r = default;
        r.X = a.X + b.X; r.Y = a.Y + b.Y;
        r.Z = a.Z + b.Z; r.W = a.W + b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_Subtraction(Quaternion a, Quaternion b)
    {
        Quaternion r = default;
        r.X = a.X - b.X; r.Y = a.Y - b.Y;
        r.Z = a.Z - b.Z; r.W = a.W - b.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_UnaryNegation(Quaternion a)
    {
        Quaternion r = default;
        r.X = -a.X; r.Y = -a.Y; r.Z = -a.Z; r.W = -a.W;
        return r;
    }

    /// <summary>
    /// Quaternion–quaternion multiplication (rotation composition).
    /// Result = a*b applied to a vector v = a.Apply(b.Apply(v)).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_Multiply(Quaternion a, Quaternion b)
    {
        Quaternion r = default;
        r.X = a.X * b.W + b.X * a.W + (a.Y * b.Z - a.Z * b.Y);
        r.Y = a.Y * b.W + b.Y * a.W + (a.Z * b.X - a.X * b.Z);
        r.Z = a.Z * b.W + b.Z * a.W + (a.X * b.Y - a.Y * b.X);
        r.W = a.W * b.W - (a.X * b.X + a.Y * b.Y + a.Z * b.Z);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_Multiply(Quaternion a, float s)
    {
        Quaternion r = default;
        r.X = a.X * s; r.Y = a.Y * s;
        r.Z = a.Z * s; r.W = a.W * s;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion op_Division(Quaternion a, Quaternion b)
    {
        // a / b = a * inverse(b). For unit quaternions inverse == conjugate;
        // we follow the BCL contract and use the full inverse.
        float ls = b.X * b.X + b.Y * b.Y + b.Z * b.Z + b.W * b.W;
        float invLs = 1f / ls;
        // Inverse(b)
        float bx = -b.X * invLs;
        float by = -b.Y * invLs;
        float bz = -b.Z * invLs;
        float bw =  b.W * invLs;

        Quaternion r = default;
        r.X = a.X * bw + bx * a.W + (a.Y * bz - a.Z * by);
        r.Y = a.Y * bw + by * a.W + (a.Z * bx - a.X * bz);
        r.Z = a.Z * bw + bz * a.W + (a.X * by - a.Y * bx);
        r.W = a.W * bw - (a.X * bx + a.Y * by + a.Z * bz);
        return r;
    }

    // -- Static API ---------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Add(Quaternion a, Quaternion b) => op_Addition(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Subtract(Quaternion a, Quaternion b) =>
        op_Subtraction(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Multiply(Quaternion a, Quaternion b) => op_Multiply(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Multiply(Quaternion a, float s) => op_Multiply(a, s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Divide(Quaternion a, Quaternion b) => op_Division(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Negate(Quaternion a) => op_UnaryNegation(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Dot(Quaternion a, Quaternion b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Conjugate(Quaternion q)
    {
        Quaternion r = default;
        r.X = -q.X; r.Y = -q.Y; r.Z = -q.Z; r.W = q.W;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Inverse(Quaternion q)
    {
        float ls = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        float invLs = 1f / ls;
        Quaternion r = default;
        r.X = -q.X * invLs;
        r.Y = -q.Y * invLs;
        r.Z = -q.Z * invLs;
        r.W =  q.W * invLs;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Normalize(Quaternion q)
    {
        float invLen = 1f / XMath.Sqrt(
            q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        Quaternion r = default;
        r.X = q.X * invLen;
        r.Y = q.Y * invLen;
        r.Z = q.Z * invLen;
        r.W = q.W * invLen;
        return r;
    }

    /// <summary>
    /// Concatenates rotations: Concatenate(a, b) applies a *then* b.
    /// Equivalent to <c>b * a</c> with quaternion multiplication.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Concatenate(Quaternion a, Quaternion b) =>
        op_Multiply(b, a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
    {
        float halfAngle = angle * 0.5f;
        float s = XMath.Sin(halfAngle);
        float c = XMath.Cos(halfAngle);
        Quaternion r = default;
        r.X = axis.X * s;
        r.Y = axis.Y * s;
        r.Z = axis.Z * s;
        r.W = c;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion CreateFromYawPitchRoll(
        float yaw, float pitch, float roll)
    {
        float halfRoll = roll * 0.5f;
        float halfPitch = pitch * 0.5f;
        float halfYaw = yaw * 0.5f;

        float sr = XMath.Sin(halfRoll);
        float cr = XMath.Cos(halfRoll);
        float sp = XMath.Sin(halfPitch);
        float cp = XMath.Cos(halfPitch);
        float sy = XMath.Sin(halfYaw);
        float cy = XMath.Cos(halfYaw);

        Quaternion r = default;
        r.X = cy * sp * cr + sy * cp * sr;
        r.Y = sy * cp * cr - cy * sp * sr;
        r.Z = cy * cp * sr - sy * sp * cr;
        r.W = cy * cp * cr + sy * sp * sr;
        return r;
    }

    /// <summary>
    /// Extracts the rotation quaternion from a rotation matrix using
    /// Shepperd's branch-on-largest-trace formulation. Matches the BCL
    /// algorithm without the <c>Vector128</c> fan-out.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion CreateFromRotationMatrix(Matrix4x4 m)
    {
        // Shepperd's method: pick the branch that maximises the diagonal
        // contribution to keep the sqrt operand far from zero. Computed
        // through scalar locals (rx, ry, rz, rw) rather than partial
        // assignments to a `Quaternion r = default`. The latter shape
        // produces SSA phis that the CPU vectorized backend collapses
        // incorrectly — every branch must write every component for the
        // merged value to be defined on every flow path.
        float trace = m.M11 + m.M22 + m.M33;
        float rx, ry, rz, rw;

        if (trace > 0f)
        {
            float s = XMath.Sqrt(trace + 1f);
            rw = s * 0.5f;
            float invS = 0.5f / s;
            rx = (m.M23 - m.M32) * invS;
            ry = (m.M31 - m.M13) * invS;
            rz = (m.M12 - m.M21) * invS;
        }
        else if (m.M11 >= m.M22 && m.M11 >= m.M33)
        {
            float s = XMath.Sqrt(1f + m.M11 - m.M22 - m.M33);
            float invS = 0.5f / s;
            rx = 0.5f * s;
            ry = (m.M12 + m.M21) * invS;
            rz = (m.M13 + m.M31) * invS;
            rw = (m.M23 - m.M32) * invS;
        }
        else if (m.M22 > m.M33)
        {
            float s = XMath.Sqrt(1f + m.M22 - m.M11 - m.M33);
            float invS = 0.5f / s;
            rx = (m.M21 + m.M12) * invS;
            ry = 0.5f * s;
            rz = (m.M32 + m.M23) * invS;
            rw = (m.M31 - m.M13) * invS;
        }
        else
        {
            float s = XMath.Sqrt(1f + m.M33 - m.M11 - m.M22);
            float invS = 0.5f / s;
            rx = (m.M31 + m.M13) * invS;
            ry = (m.M32 + m.M23) * invS;
            rz = 0.5f * s;
            rw = (m.M12 - m.M21) * invS;
        }

        Quaternion r = default;
        r.X = rx; r.Y = ry; r.Z = rz; r.W = rw;
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Lerp(Quaternion a, Quaternion b, float t)
    {
        float t1 = 1f - t;
        // Pick shortest path
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
        Quaternion r = default;
        if (dot >= 0f)
        {
            r.X = t1 * a.X + t * b.X;
            r.Y = t1 * a.Y + t * b.Y;
            r.Z = t1 * a.Z + t * b.Z;
            r.W = t1 * a.W + t * b.W;
        }
        else
        {
            r.X = t1 * a.X - t * b.X;
            r.Y = t1 * a.Y - t * b.Y;
            r.Z = t1 * a.Z - t * b.Z;
            r.W = t1 * a.W - t * b.W;
        }

        // Normalize
        float invLen = 1f / XMath.Sqrt(
            r.X * r.X + r.Y * r.Y + r.Z * r.Z + r.W * r.W);
        r.X *= invLen; r.Y *= invLen; r.Z *= invLen; r.W *= invLen;
        return r;
    }

    /// <summary>
    /// Spherical linear interpolation. Matches BCL semantics including
    /// the small-angle linear-interp fallback at the threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        const float SlerpEpsilon = 1e-6f;
        float cosOmega = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
        bool flip = false;
        if (cosOmega < 0f)
        {
            flip = true;
            cosOmega = -cosOmega;
        }

        float s1, s2;
        if (cosOmega > 1f - SlerpEpsilon)
        {
            // Linear interpolation
            s1 = 1f - t;
            s2 = flip ? -t : t;
        }
        else
        {
            float omega = XMath.Acos(cosOmega);
            float invSinOmega = 1f / XMath.Sin(omega);
            s1 = XMath.Sin((1f - t) * omega) * invSinOmega;
            s2 = flip
                ? -XMath.Sin(t * omega) * invSinOmega
                :  XMath.Sin(t * omega) * invSinOmega;
        }

        Quaternion r = default;
        r.X = s1 * a.X + s2 * b.X;
        r.Y = s1 * a.Y + s2 * b.Y;
        r.Z = s1 * a.Z + s2 * b.Z;
        r.W = s1 * a.W + s2 * b.W;
        return r;
    }
}
