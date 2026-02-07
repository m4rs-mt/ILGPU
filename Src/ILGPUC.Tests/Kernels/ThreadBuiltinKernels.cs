// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreadBuiltinKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels that exercise Metal's kernel built-in parameter threading.
///
/// On Metal, GroupIndexValue/GridIndexValue/GroupDimensionValue/GridDimensionValue all
/// reference [[attribute]]-qualified entry-point parameters that are out of scope inside
/// [NoInline] helpers. MetalKernelLowering threads all four as explicit Int32 parameters.
/// </summary>
static class ThreadBuiltinKernels
{
    /// <summary>
    /// [NoInline] helper that reads all four Metal-threaded built-ins.
    /// After MetalKernelLowering, the IR signature of this method gains four extra
    /// parameters: groupIdx, gridIdx, groupDim, gridDim.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ComputeGlobalIndex()
    {
        // Each of these is an IR DeviceConstantValue (all Int32 at the IR level).
        // In the Metal entry point they map to:
        //   Group.Index     -> thread_position_in_threadgroup.x
        //   Grid.Index      -> threadgroup_position_in_grid.x
        //   Group.Dimension -> threads_per_threadgroup.x
        //   Grid.Dimension  -> threadgroups_per_grid.x
        int groupIdx = Group.Index;
        int gridIdx  = (int)Grid.Index;
        int groupDim = Group.Dimension;
        int gridDim  = (int)Grid.Dimension;

        // Use all four values so none can be eliminated as dead code.
        // Verify total thread count equals gridDim * groupDim; clamp to safe range.
        int total  = gridDim * groupDim;
        int global = gridIdx * groupDim + groupIdx;
        return global < total ? global : total - 1;
    }

    /// <summary>
    /// Kernel that calls a [NoInline] helper which reads all four thread built-ins:
    /// Group.Index, Grid.Index, Group.Dimension, Grid.Dimension.
    /// Exercises MetalKernelLowering parameter threading for all four values.
    /// </summary>
    public static void AllBuiltinsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = ComputeGlobalIndex();
    }
}
