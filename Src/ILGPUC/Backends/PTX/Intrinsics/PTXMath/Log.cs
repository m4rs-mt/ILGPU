// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Log.cs
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
    /// <inheritdoc cref="XMath.Log(double, double)"/>
    public static double BinaryLogFloat64(double value, double newBase)
    {
        if (XMath.OptimizePerformance)
            return LogFloat64(value) * RcpFloat64(LogFloat64(newBase));
        return GenericMath.Log(value, newBase);
    }

    /// <inheritdoc cref="XMath.Log(float, float)"/>
    public static float BinaryLogFloat32(float value, float newBase)
    {
        if (XMath.OptimizePerformance)
            return LogFloat32(value) * RcpFloat32(LogFloat32(newBase));
        return GenericMath.Log(value, newBase);
    }

    /// <inheritdoc cref="XMath.Log(double)"/>
    public static double LogFloat64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Log(value);

        if (XMath.OptimizePerformance)
            return Log2Float64(value) * XMath.OneOverLog2ED;

        return Cordic.Log(value);
    }

    /// <inheritdoc cref="XMath.Log(float)"/>
    public static float LogFloat32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.OptimizePerformance)
                return LD.FastLog(value);
            return LD.Log(value);
        }

        if (XMath.OptimizePerformance)
            return Log2Float32(value) * XMath.OneOverLog2E;

        return Cordic.Log(value);
    }

    /// <inheritdoc cref="XMath.Log10(double)"/>
    public static double Log10Float64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Log10(value);
        return LogFloat64(value) * XMath.OneOverLn10D;
    }

    /// <inheritdoc cref="XMath.Log10(float)"/>
    public static float Log10Float32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.OptimizePerformance)
                return LD.FastLog10(value);
            return LD.Log10(value);
        }
        return LogFloat32(value) * XMath.OneOverLn10;
    }

    /// <inheritdoc cref="XMath.Log2(double)"/>
    public static double Log2Float64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Log2(value);
        return LogFloat64(value) * XMath.OneOverLn2D;
    }

    /// <inheritdoc cref="XMath.Log2(float)"/>
    public static float Log2Float32(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.OptimizePerformance)
                return LD.FastLog2(value);
            return LD.Log2(value);
        }
        else
        {
            float result;
            if (XMath.FlushToZero)
                CudaAsm.Emit("lg2.approx.ftz.f32 {0}, {1}", out result, value);
            else
                CudaAsm.Emit("lg2.approx.f32 {0}, {1}", out result, value);
            return result;
        }
    }
}
