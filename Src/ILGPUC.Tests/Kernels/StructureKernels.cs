// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for structure access tests.
/// </summary>
static class StructureKernels
{
    static TestStruct ModifyStruct(TestStruct s)
    {
        s.X = s.X + 1;
        s.Y = s.Y + 10L;
        s.Z = (short)(s.Z + 100);
        s.W = s.W + 1000;
        return s;
    }

    /// <summary>
    /// Read/write struct fields.
    /// </summary>
    public static void StructFieldAccessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        TestStruct input)
    {
        // Store a combination of struct fields
        data[index] = input.X + (int)input.Y + (int)input.Z + input.W;
    }

    /// <summary>
    /// Pass struct to helper by value, modify, and use returned value.
    /// </summary>
    public static void StructPassByValueKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        TestStruct input)
    {
        TestStruct modified = ModifyStruct(input);
        data[index] = modified.X + (int)modified.Y + (int)modified.Z + modified.W;
    }

    /// <summary>
    /// Build a struct locally (alloca) with per-field writes, then output
    /// a summary. This exercises LoadFieldAddress on local struct allocas,
    /// which is the vectorized LFA path on CPU.
    /// </summary>
    public static void StructLocalBuildKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        TestStruct local;
        local.X = value;
        local.Y = value * 2L;
        local.Z = (short)(value * 3);
        local.W = value * 4;
        data[index] = local.X + (int)local.Y + (int)local.Z + local.W;
    }
}
