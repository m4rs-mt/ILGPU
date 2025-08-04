// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BitOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime.Cuda;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.PopCount(uint)"/>
    public static int PopCountInt32(uint value)
    {
        CudaAsm.Emit("popc.b32 {0}, {1}", out int result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.PopCount(ulong)"/>
    public static int PopCountInt64(ulong value)
    {
        CudaAsm.Emit("popc.b64 {0}, {1}", out int result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.LeadingZeroCount(uint)"/>
    public static int CLZInt32(uint value)
    {
        CudaAsm.Emit("clz.b32 {0}, {1}", out int result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.LeadingZeroCount(ulong)"/>
    public static int CLZInt64(ulong value)
    {
        CudaAsm.Emit("clz.b64 {0}, {1}", out int result, value);
        return result;
    }

    /// <inheritdoc cref="XMath.TrailingZeroCount(int)"/>
    public static int CTZInt32(int value) =>
        PopCountInt32((uint)((value & -value) - 1));

    /// <inheritdoc cref="XMath.TrailingZeroCount(long)"/>
    public static int CTZInt64(long value) =>
        PopCountInt64((ulong)((value & -value) - 1L));
}
