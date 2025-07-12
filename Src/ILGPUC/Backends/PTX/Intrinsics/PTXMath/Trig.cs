// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Trig.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;
using ILGPUC.Intrinsic;
using LD = ILGPU.Runtime.Cuda.CudaLibDevice;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.Sin(double)" />
    public static double SinFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Sin(value);

        return Cordic.Sin(value);
    }

    /// <inheritdoc cref="XMath.Sin(float)" />
    public static float SinFloat32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.OptimizePerformance)
                return LD.FastSin(value);
            return LD.Sin(value);
        }

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "sin.approx.ftz.f32 {0} {1}"
            : "sin.approx.f32 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <summary cref="XMath.Asin(double)" />
    public static double AsinFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Asin(value);

        return AsinSoftware(value);
    }

    /// <summary cref="XMath.Asin(double)" />
    private static double AsinSoftware(double value)
    {
        if (XMath.IsNaN(value) | value < -1.0 | value > 1.0)
            return double.NaN;

        if (value == 1.0)
            return XMath.PID;
        else if (value == -1.0)
            return -XMath.PID;

        double arg = value * RsqrtFloat64(1.0 - value * value);
        return AtanFloat64(arg);
    }

    /// <summary cref="XMath.Asin(float)" />
    public static float AsinFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Asin(value);

        return AsinSoftware(value);
    }

    /// <summary cref="XMath.Asin(float)" />
    private static float AsinSoftware(float value)
    {
        if (XMath.IsNaN(value) | value < -1.0f | value > 1.0f)
            return float.NaN;

        if (value == 1.0f)
            return XMath.PI;
        else if (value == -1.0f)
            return -XMath.PI;

        float arg = value * RsqrtFloat32(1.0f - value * value);
        return AtanFloat32(arg);
    }

    /// <inheritdoc cref="XMath.Asinh(double)" />
    public static double AsinhFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Asinh(value);

        if (XMath.IsNaN(value))
            return double.NaN;
        return value - PowFloat64(value, 3.0) / 6.0;
    }

    /// <inheritdoc cref="XMath.Asinh(double)" />
    public static float AsinhFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Asinh(value);

        if (XMath.IsNaN(value))
            return float.NaN;
        return value - PowFloat32(value, 3.0f) / 6.0f;
    }

    /// <inheritdoc cref="XMath.Sinh(double)" />
    public static double SinhFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Sinh(value);

        return 0.5 * (ExpFloat64(value) - ExpFloat64(-value));
    }

    /// <inheritdoc cref="XMath.Sinh(float)" />
    public static float SinhFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Sinh(value);

        return 0.5f * (ExpFloat32(value) - ExpFloat32(-value));
    }

    /// <inheritdoc cref="XMath.Cos(double)" />
    public static double CosFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Cos(value);

        return Cordic.Cos(value);
    }

    /// <inheritdoc cref="XMath.Cos(float)" />
    public static float CosFloat32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.OptimizePerformance)
                return LD.FastCos(value);
            return LD.Cos(value);
        }

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "cos.approx.ftz.f32 {0} {1}"
            : "cos.approx.f32 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Cosh(double)" />
    public static double CoshFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Cosh(value);

        return 0.5 * (ExpFloat64(value) + ExpFloat64(-value));
    }

    /// <inheritdoc cref="XMath.Cosh(float)" />
    public static float CoshFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Cosh(value);

        return 0.5f * (ExpFloat32(value) + ExpFloat32(-value));
    }

    /// <inheritdoc cref="XMath.Acos(double)" />
    public static double AcosFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Acos(value);

        return XMath.PID - AsinFloat64(value);
    }

    /// <inheritdoc cref="XMath.Acos(float)" />
    public static float AcosFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Acos(value);

        return XMath.PI - AsinFloat32(value);
    }

    /// <inheritdoc cref="XMath.Acosh(double)" />
    public static double AcoshFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Acosh(value);

        return LogFloat64(value + SqrtFloat64(value * value - 1.0f));
    }

    /// <inheritdoc cref="XMath.Acosh(float)" />
    public static float AcoshFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Acosh(value);

        return LogFloat32(value + SqrtFloat32(value * value - 1.0f));
    }

    /// <inheritdoc cref="XMath.Tan(double)" />
    public static double TanFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Tan(value);

        return Cordic.Tan(value);
    }

    /// <inheritdoc cref="XMath.Tan(float)" />
    public static float TanFloat32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.FastMath)
                return LD.FastTan(value);
            return LD.Tan(value);
        }

        return Cordic.Tan(value);
    }

    /// <inheritdoc cref="XMath.Tanh(double)" />
    public static double TanhFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Tanh(value);

        return TanhFloat64Software(value);
    }

    /// <inheritdoc cref="XMath.Tanh(double)" />
    private static double TanhFloat64Software(double value)
    {
        if (XMath.IsNaN(value))
            return value;
        else if (value == double.PositiveInfinity)
            return 1.0;
        else if (value == double.NegativeInfinity)
            return -1.0;

        var exp = ExpFloat64(2.0 * value);
        var denominator = XMath.Rcp(exp + 1.0);
        return (exp - 1.0) * denominator;
    }

    /// <inheritdoc cref="XMath.Tanh(float)" />
    public static float TanhFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Tanh(value);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "tanh.approx.ftz.f32 {0} {1}"
            : "tanh.approx.f32 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Tanh(Half)" />
    public static Half TanhFloat16(Half value)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_75)
            return (Half)TanhFloat32(value);

        CudaAsm.Emit("tanh.approx.f16 {0}, {1}", out Half result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Atan(double)" />
    public static double AtanFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Atan(value);

        return Cordic.Atan(value);
    }

    /// <inheritdoc cref="XMath.Atan(float)" />
    public static float AtanFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Atan(value);

        return Cordic.Atan(value);
    }

    /// <inheritdoc cref="XMath.Atanh(double)" />
    public static double AtanhFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Atanh(value);

        if (AbsFloat64(value) >= 1.0)
            return float.NaN;
        return value * RcpFloat64(SqrtFloat64(1.0 - value * value));
    }

    /// <inheritdoc cref="XMath.Atanh(float)" />
    public static float AtanhFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Atanh(value);

        if (AbsFloat32(value) >= 1.0f)
            return float.NaN;
        return value * RcpFloat32(SqrtFloat32(1.0f - value * value));
    }

    /// <inheritdoc cref="XMath.Atan2(double, double)" />
    public static double Atan2Float64(double y, double x)
    {
        if (LD.IsSupported)
            return LD.Atan(y, x);

        return Cordic.Atan2(y, x);
    }

    /// <inheritdoc cref="XMath.Atan2(float, float)" />
    public static float Atan2Float32(float y, float x)
    {
        if (LD.IsSupported)
            return LD.Atan(y, x);

        return Cordic.Atan2(y, x);
    }

    /// <inheritdoc cref="XMath.SinCos(double)" />
    public static (double, double) SinCosFloat64(double value)
    {
        double sin, cos;
        if (LD.IsSupported)
            LD.SinCos(value, out sin, out cos);
        else
        {
            sin = SinFloat64(value);
            cos = CosFloat64(value);
        }
        return (sin, cos);
    }

    /// <inheritdoc cref="XMath.SinCos(float)" />
    public static (float, float) SinCosFloat32(float value)
    {
        float sin, cos;
        if (LD.IsSupported)
            LD.SinCos(value, out sin, out cos);
        else
        {
            sin = SinFloat32(value);
            cos = CosFloat32(value);
        }
        return (sin, cos);
    }
}
