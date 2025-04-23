// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sign.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.CopySign(double, double)" />
    public static double CopySignFloat64(double x, double y)
    {
        CudaAsm.Emit("copysign.f64 {0}, {1}, {2}", out double result, x, y);
        return result;
    }

    /// <inheritdoc cref="XMath.CopySign(float, float)" />
    public static float CopySignFloat32(float x, float y)
    {
        CudaAsm.Emit("copysign.f32 {0}, {1}, {2}", out float result, x, y);
        return result;
    }
}
