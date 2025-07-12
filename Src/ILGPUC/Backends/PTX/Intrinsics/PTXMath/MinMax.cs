// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MinMax.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;
using Half = ILGPU.Half;
using LD = ILGPU.Runtime.Cuda.CudaLibDevice;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.Min(double, double)"/>
    public static double MinFloat64(double first, double second)
    {
        if (LD.IsSupported)
            return LD.Min(first, second);

        CudaAsm.Emit("min.f64 {0}, {1}, {2}", out double result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(float, float)"/>
    public static float MinFloat32(float first, float second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Min(first, second);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "min.ftz.f32 {0}, {1}, {2}"
            : "min.f32 {0}, {1}, {2}",
            out float result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(Half, Half)"/>
    public static Half MinFloat16(Half first, Half second)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_80)
            return HalfExtensions.MinFP32(first, second);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "min.ftz.f16 {0}, {1}, {2}"
            : "min.f16 {0}, {1}, {2}",
            out Half result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(sbyte, sbyte)"/>
    public static sbyte MinInt8(sbyte first, sbyte second) =>
        (sbyte)MinInt16(first, second);

    /// <inheritdoc cref="XMath.Min(short, short)"/>
    public static short MinInt16(short first, short second)
    {
        CudaAsm.Emit("min.s16 {0}, {1}, {2}", out short result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(int, int)"/>
    public static int MinInt32(int first, int second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Min(first, second);

        CudaAsm.Emit("min.s32 {0}, {1}, {2}", out int result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(long, long)"/>
    public static long MinInt64(long first, long second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Min(first, second);

        CudaAsm.Emit("min.s64 {0}, {1}, {2}", out long result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(byte, byte)"/>
    public static byte MinUInt8(byte first, byte second) =>
        (byte)MinUInt16(first, second);

    /// <inheritdoc cref="XMath.Min(ushort, ushort)"/>
    public static ushort MinUInt16(ushort first, ushort second)
    {
        CudaAsm.Emit("min.u16 {0}, {1}, {2}", out ushort result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(uint, uint)"/>
    public static uint MinUInt32(uint first, uint second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Min(first, second);

        CudaAsm.Emit("min.u32 {0}, {1}, {2}", out uint result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Min(ulong, ulong)"/>
    public static ulong MinUInt64(ulong first, ulong second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Min(first, second);

        CudaAsm.Emit("min.u64 {0}, {1}, {2}", out ulong result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(double, double)"/>
    public static double MaxFloat64(double first, double second)
    {
        if (LD.IsSupported)
            return LD.Max(first, second);

        CudaAsm.Emit("max.f64 {0}, {1}, {2}", out double result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(float, float)"/>
    public static float MaxFloat32(float first, float second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Max(first, second);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "max.ftz.f32 {0}, {1}, {2}"
            : "max.f32 {0}, {1}, {2}",
            out float result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(Half, Half)"/>
    public static Half MaxFloat16(Half first, Half second)
    {
        if (CudaArchitecture.Current < CudaArchitecture.SM_80)
            return HalfExtensions.MaxFP32(first, second);

        CudaAsm.Emit(
            XMath.FlushToZero
            ? "max.ftz.f16 {0}, {1}, {2}"
            : "max.f16 {0}, {1}, {2}",
            out Half result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(sbyte, sbyte)"/>
    public static sbyte MaxInt8(sbyte first, sbyte second) =>
        (sbyte)MaxInt16(first, second);

    /// <inheritdoc cref="XMath.Max(short, short)"/>
    public static short MaxInt16(short first, short second)
    {
        CudaAsm.Emit("max.s16 {0}, {1}, {2}", out short result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(short, short)"/>
    public static int MaxInt32(int first, int second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Max(first, second);

        CudaAsm.Emit("max.s32 {0}, {1}, {2}", out int result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(long, long)"/>
    public static long MaxInt64(long first, long second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Max(first, second);

        CudaAsm.Emit("max.s64 {0}, {1}, {2}", out int result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(byte, byte)"/>
    public static byte MaxUInt8(byte first, byte second) =>
        (byte)MaxUInt16(first, second);

    /// <inheritdoc cref="XMath.Max(ushort, ushort)"/>
    public static ushort MaxUInt16(ushort first, ushort second)
    {
        CudaAsm.Emit("max.u16 {0}, {1}, {2}", out ushort result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(uint, uint)"/>
    public static uint MaxUInt32(uint first, uint second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Max(first, second);

        CudaAsm.Emit("max.u32 {0}, {1}, {2}", out uint result, first, second);
        return result;
    }

    /// <inheritdoc cref="XMath.Max(ulong, ulong)"/>
    public static ulong MaxUInt64(ulong first, ulong second)
    {
        if (LD.IsSupported & !XMath.OptimizePerformance)
            return LD.Max(first, second);

        CudaAsm.Emit("max.u64 {0}, {1}, {2}", out ulong result, first, second);
        return result;
    }
}
