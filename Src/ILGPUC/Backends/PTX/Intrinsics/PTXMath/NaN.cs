// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaN.cs
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
    /// <inheritdoc cref="XMath.IsNaN(double)"/>
    public static bool IsNaNFloat64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsNaN(value) != 0;

        CudaAsm.Emit("testp.notanumber.f64 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsNaN(float)"/>
    public static bool IsNaNFloat32(float value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsNaN(value) != 0;

        CudaAsm.Emit("testp.notanumber.f32 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsNaN(Half)"/>
    public static bool IsNaNFloat16(Half value) =>
        HalfExtensions.IsNaN(value);

    /// <inheritdoc cref="XMath.IsInfinity(double)"/>
    public static bool IsInfinityFloat64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsInfinity(value) != 0;

        CudaAsm.Emit("testp.infinite.f64 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsInfinity(float)"/>
    public static bool IsInfinityFloat32(float value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsInfinity(value) != 0;

        CudaAsm.Emit("testp.infinite.f32 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsInfinity(Half)"/>
    public static bool IsInfinityFloat16(Half value) =>
        HalfExtensions.IsInfinity(value);

    /// <inheritdoc cref="XMath.IsFinite(double)"/>
    public static bool IsFiniteFloat64(double value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsFinite(value) != 0;

        CudaAsm.Emit("testp.finite.f64 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsFinite(float)"/>
    public static bool IsFiniteFloat32(float value)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.IsFinite(value) != 0;

        CudaAsm.Emit("testp.finite.f32 {0}, {1}", out bool result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.IsFinite(Half)"/>
    public static bool IsFiniteFloat16(Half value) =>
        HalfExtensions.IsFinite(value);
}
