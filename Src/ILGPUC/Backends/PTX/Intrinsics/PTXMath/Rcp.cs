// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Rcp.cs
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
    /// <inheritdoc cref="XMath.Rcp(double)"/>
    public static double RcpFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.RcpRoundZero(value);

        CudaAsm.Emit(
            XMath.OptimizePerformance
            ? "rcp.approx.ftz.f64 {0}, {1}"
            : "rcp.rn.f64 {0}, {1}",
            out double result,
            value);
        return result;
    }

    /// <inheritdoc cref="XMath.Rcp(float)"/>
    public static float RcpFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.RcpRoundZero(value);

        CudaAsm.Emit(
            XMath.OptimizePerformance
            ? "rcp.approx.ftz.f32 {0}, {1}"
            : "rcp.rn.f32 {0}, {1}",
            out float result,
            value);
        return result;
    }
}
