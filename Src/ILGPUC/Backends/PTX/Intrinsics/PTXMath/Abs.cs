// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Abs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;
using Half = ILGPU.Half;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.Abs(double)"/>
    public static double AbsFloat64(double value)
    {
        CudaAsm.Emit("abs.f64 {0}, {1}", out double result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Abs(float)"/>
    public static float AbsFloat32(float value)
    {
        CudaAsm.Emit("abs.f32 {0}, {1}", out float result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Abs(Half)"/>
    public static Half AbsFloat16(Half value)
    {
        Half result;
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            result = (Half)AbsFloat32(value);
        else
            CudaAsm.Emit("abs.f16 {0}, {1}", out result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Abs(sbyte)"/>
    public static sbyte AbsInt8(sbyte value) => (sbyte)AbsInt16(value);

    /// <inheritdoc cref="XMath.Abs(int)"/>
    public static short AbsInt16(short value)
    {
        CudaAsm.Emit("abs.s16 {0}, {1}", out short result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Abs(int)"/>
    public static int AbsInt32(int value)
    {
        CudaAsm.Emit("abs.s32 {0}, {1}", out int result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.Abs(long)"/>
    public static long AbsInt64(long value)
    {
        CudaAsm.Emit("abs.s64 {0}, {1}", out long result, value);
        return result;
    }
}
