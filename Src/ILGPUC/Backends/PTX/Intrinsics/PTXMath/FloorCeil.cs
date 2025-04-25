// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloorCeil.cs
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
    /// <inheritdoc cref="XMath.Floor(double)"/>
    public static double FloorFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Floor(value);

        CudaAsm.Emit("cvt.rmi.f64.f64 {0}, {1}", out double result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Floor(float)"/>
    public static float FloorFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Floor(value);

        CudaAsm.Emit("cvt.rmi.f32.f32 {0}, {1}", out float result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Ceiling(double)"/>
    public static double CeilingFloat64(double value)
    {
        if (LD.IsSupported)
            return LD.Ceil(value);

        CudaAsm.Emit("cvt.rpi.f64.f64 {0}, {1}", out double result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Ceiling(float)"/>
    public static float CeilingFloat32(float value)
    {
        if (LD.IsSupported)
            return LD.Ceil(value);

        CudaAsm.Emit("cvt.rpi.f32.f32 {0}, {1}", out float result, value);
        return result;
    }
}
