// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Rem.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.Intrinsic;
using LD = ILGPU.Runtime.Cuda.CudaLibDevice;

namespace ILGPUC.Backends.PTX.Intrinsics;

partial class PTXMath
{
    /// <inheritdoc cref="XMath.Rem(double, double)"/>
    public static double RemFloat64(double x, double y)
    {
        if (LD.IsSupported)
            return LD.Fmod(x, y);
        return GenericMath.Rem(x, y);
    }

    /// <inheritdoc cref="XMath.Rem(float, float)"/>
    public static float RemFloat32(float x, float y)
    {
        if (LD.IsSupported)
            return LD.Fmod(x, y);
        return GenericMath.Rem(x, y);
    }

    /// <inheritdoc cref="XMath.IEEERemainder(double, double)"/>
    public static double IEEERemainderFloat64(double x, double y) =>
        GenericMath.IEEERemainder(x, y);

    /// <inheritdoc cref="XMath.IEEERemainder(float, float)"/>
    public static float IEEERemainderFloat32(float x, float y) =>
        GenericMath.IEEERemainder(x, y);
}
