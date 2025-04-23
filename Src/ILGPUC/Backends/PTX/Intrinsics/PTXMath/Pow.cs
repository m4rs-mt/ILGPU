// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Pow.cs
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
    /// <inheritdoc cref="XMath.Pow(double, double)"/>
    public static double PowDouble(double @base, double exp)
    {
        if (LD.IsSupported)
            return LD.Pow(@base, exp);
        return GenericMath.Pow(@base, exp);
    }

    /// <inheritdoc cref="XMath.Pow(float, float)"/>
    public static float PowFloat(float @base, float exp)
    {
        if (LD.IsSupported)
        {
            if (XMath.FastMath)
                return LD.FastPow(@base, exp);
            return LD.Pow(@base, exp);
        }
        return GenericMath.Pow(@base, exp);
    }

    /// <inheritdoc cref="XMath.Exp(double)" />
    public static double ExpDouble(double value)
    {
        if (LD.IsSupported)
            return LD.Exp(value);
        return Cordic.Exp(value);
    }

    /// <inheritdoc cref="XMath.Exp(float)" />
    public static float ExpFloat(float value)
    {
        if (LD.IsSupported)
        {
            if (XMath.FastMath)
                return LD.FastExp(value);
            return LD.Exp(value);
        }
        if (XMath.OptimizePerformance)
            return Exp2Float(value * XMath.OneOverLn2);
        return Cordic.Exp(value);
    }

    /// <inheritdoc cref="XMath.Exp2(double)" />
    public static double Exp2Double(double value)
    {
        if (LD.IsSupported)
            return LD.Exp2(value);
        return ExpDouble(value * XMath.OneOverLog2ED);
    }

    /// <inheritdoc cref="XMath.Exp2(float)" />
    public static float Exp2Float(float value)
    {
        float result;
        if (XMath.OptimizePerformance)
            CudaAsm.Emit("ex2.approx.ftz.f32 {0}, {1}", out result, value);
        else if (LD.IsSupported)
            result = LD.Exp2(value);
        else
            CudaAsm.Emit("ex2.approx.f32 {0}, {1}", out result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Exp2(Half)" />
    public static Half Exp2Half(Half value)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_75)
            return (Half)Exp2Float(value);

        CudaAsm.Emit("ex2.approx.f16 {0}, {1}", out Half result, value);
        return result;
    }
}
