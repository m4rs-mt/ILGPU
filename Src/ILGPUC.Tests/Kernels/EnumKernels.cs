// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: EnumKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for enum conversion tests.
/// </summary>
static class EnumKernels
{
    enum ByteEnum : byte { A = 1, B = 2 }
    enum ShortEnum : short { X = 10, Y = 20 }
    enum IntEnum { Foo = 100, Bar = 200 }
    enum LongEnum : long { Big = 1000L, Bigger = 2000L }

    /// <summary>
    /// Cast ByteEnum to underlying byte type and store as int.
    /// </summary>
    public static void ByteEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ByteEnum e = ByteEnum.A;
        data[index] = (int)(byte)e;
    }

    /// <summary>
    /// Cast ShortEnum to underlying short type and store as int.
    /// </summary>
    public static void ShortEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ShortEnum e = ShortEnum.Y;
        data[index] = (int)(short)e;
    }

    /// <summary>
    /// Cast IntEnum to underlying int type and store.
    /// </summary>
    public static void IntEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        IntEnum e = IntEnum.Bar;
        data[index] = (int)e;
    }

    /// <summary>
    /// Cast LongEnum to underlying long type and store low bits as int.
    /// </summary>
    public static void LongEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        LongEnum e = LongEnum.Bigger;
        data[index] = (int)(long)e;
    }
}
