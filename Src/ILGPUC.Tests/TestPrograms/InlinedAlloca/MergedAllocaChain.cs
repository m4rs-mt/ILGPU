// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MergedAllocaChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: MergedAllocaChain
// An address-taken local (alloca) reached through a cast, written by a store, passed
// to a call that the inliner splits the block around, and read back by a load, all
// inside a loop body that gives SimplifyControlFlow chains to merge.
//
// If the alloca's def-use set is split across two physical allocas while inlining,
// SSA construction promotes the one the load points at and the kernel reads the
// local's initial value instead of the computed one.
//
// Accumulate(s, 5) = (s + 5) * 2, so the loop sums 30 + 32 + 34.
// Expected output: 96 96 96 96

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    // Takes the local's address: forces an alloca in the caller and an
    // addrspacecast at the call site.
    static int Bump(ref int slot, int amount)
    {
        slot += amount;
        return slot;
    }

    // alloca -> store -> call -> load, all in one inlinable body.
    static int Accumulate(int seed, int amount)
    {
        int acc = seed;
        int direct = Bump(ref acc, amount);
        return acc + direct;
    }

    public static void MergedAllocaChainKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int seed)
    {
        int total = 0;
        // The loop gives SimplifyControlFlow blocks to merge around the
        // inliner's split exit blocks.
        for (int i = 0; i < 3; ++i)
            total += Accumulate(seed + i, 5);
        data[index] = total;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.MergedAllocaChainKernel(index, buffer.View, 10));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
