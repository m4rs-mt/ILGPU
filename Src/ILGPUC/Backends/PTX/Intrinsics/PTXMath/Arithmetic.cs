// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
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
    /// <summary>
    /// Internal wrapper around neg instructions.
    /// </summary>
    public static Half BinaryNegHalf(Half value)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            return HalfExtensions.Neg(value);

        CudaAsm.Emit("neg.f16 {0}, {1}", out Half result, value);
        return result;
    }

    /// <summary>
    /// Internal wrapper around add instructions.
    /// </summary>
    public static Half BinaryAddHalf(Half x, Half y)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            return HalfExtensions.AddFP32(x, y);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "add.f16 {0}, {1}, {2}"
            : "add.ftz.f16 {0}, {1}, {2}",
            out Half result, x, y);
        return result;
    }

    /// <summary>
    /// Internal wrapper around sub instructions.
    /// </summary>
    public static Half BinarySubHalf(Half x, Half y)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            return HalfExtensions.SubFP32(x, y);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "sub.f16 {0}, {1}, {2}"
            : "sub.ftz.f16 {0}, {1}, {2}",
            out Half result, x, y);
        return result;
    }

    /// <summary>
    /// Internal wrapper around mul instructions.
    /// </summary>
    public static Half BinaryMulHalf(Half x, Half y)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            return HalfExtensions.MulFP32(x, y);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "mul.f16 {0}, {1}, {2}"
            : "mul.ftz.f16 {0}, {1}, {2}",
            out Half result, x, y);
        return result;
    }

    /// <summary>
    /// Internal wrapper around div instructions.
    /// </summary>
    public static Half BinaryDivHalf(Half x, Half y) =>
        HalfExtensions.DivFP32(x, y);

    /// <summary>
    /// Internal wrapper around div instructions.
    /// </summary>
    public static Half TernaryMultiplyAddHalf(Half a, Half b, Half c)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_53)
            return HalfExtensions.FmaFP32(a, b, c);

        CudaAsm.Emit("fma.rn.f16 {0}, {1}, {2}, {3}", out Half result, a, b, c);
        return result;
    }
}
