// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpSpinLockGroups.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpSpinLockGroups
// Every warp of a launch whose groups hold several warps takes a WarpSpinLock
// in turn. The first lane of the warp holding the lock adds the warp size to a
// counter and every thread counts itself before the lock is released. The
// lock waits with Warp.Barrier(), so the warps of a group that wait for the
// lock never block the warp holding it.
// Expected output: lanes 128, threads 128

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Synchronization;

static class Kernels
{
    public static void WarpSpinLockGroupsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> state)
    {
        var spinLock = new WarpSpinLock(ref state[0]);
        spinLock.Wait();
        if (Warp.IsFirstLane)
            Atomic.Add(ref state[1], Warp.Dimension);
        Atomic.Add(ref state[2], 1);
        Warp.Barrier();
        spinLock.Release();
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        const int Length = 128;
        using var state = stream.Allocate1D<int>(3);
        state.MemSetToZero(stream);
        stream.Launch((Index1D)Length, index =>
            Kernels.WarpSpinLockGroupsKernel(index, state.View));
        stream.Synchronize();

        var values = state.GetAsArray1D();
        Console.WriteLine($"lanes {values[1]}");
        Console.WriteLine($"threads {values[2]}");
    }
}
