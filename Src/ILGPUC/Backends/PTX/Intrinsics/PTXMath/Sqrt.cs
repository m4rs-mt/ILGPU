// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sqrt.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;
using LD = ILGPU.Runtime.Cuda.CudaLibDevice;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.Sqrt(double)" />
    public static double SqrtDouble(double value)
    {
        if (LD.IsSupported)
            return LD.SqrtRoundZero(value);

        CudaAsm.Emit("sqrt.rn.f64 {0} {1}", out double result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Sqrt(float)" />
    public static float SqrtFloat(float value)
    {
        if (LD.IsSupported)
            return LD.SqrtRoundZero(value);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "sqrt.approx.ftz.f32 {0} {1}"
            : "sqrt.rn.f32 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Rsqrt(double)" />
    public static double RsqrtDouble(double value)
    {
        if (LD.IsSupported)
            return LD.Rsqrt(value);

        if (!XMath.OptimizePerformance)
            return RcpDouble(SqrtDouble(value));

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "rsqrt.approx.ftz.f64 {0} {1}"
            : "rsqrt.approx.f64 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Rsqrt(float)" />
    public static float RsqrtFloat(float value)
    {
        if (LD.IsSupported)
            return LD.Rsqrt(value);

        if (!XMath.OptimizePerformance)
            return RcpFloat(SqrtFloat(value));

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "rsqrt.approx.ftz.f32 {0} {1}"
            : "rsqrt.approx.f32 {0} {1}",
            out float result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Cbrt(double)" />
    public static double Cbrt(double value)
    {
        if (LD.IsSupported)
            return LD.Cbrt(value);
        return PowDouble(value, 1.0 / 3.0);
    }

    /// <inheritdoc cref="XMath.Cbrt(float)" />
    public static float Cbrt(float value)
    {
        if (LD.IsSupported)
            return LD.Cbrt(value);
        return PowFloat(value, 1.0f / 3.0f);
    }
}
