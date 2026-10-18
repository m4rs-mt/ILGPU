// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpBarrierDivergent.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpBarrierDivergent
// Only the first warp of each group reaches Warp.Barrier(). Its lanes exchange
// values through shared memory, reversing each run of 8 lanes, and then
// release the other warps, which spin until a first warp has passed its
// barrier and compute the same permutation directly. A barrier for the whole
// group would wait for the spinning warps and never return. Input is 10 + i;
// the program prints output[0..7] and the number of i with
// output[i] != input[i ^ 7].
// Expected output: 17 16 15 14 13 12 11 10 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpBarrierDivergentKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> released)
    {
        var shared = Group.GetSharedMemory<int>(64);

        // The length is a multiple of 64, so this branch is uniform per warp
        if (index.X < output.IntLength)
        {
            if (Warp.Index == 0)
            {
                // The lanes of a warp hold consecutive indices, so slot ^ 7 is
                // the slot of another lane of the same warp (warp size <= 64)
                int slot = index.X % 64;
                shared[slot] = input[index];
                Warp.Barrier();
                output[index] = shared[slot ^ 7];
                Atomic.Exchange(ref released[0], 1);
            }
            else
            {
                while (Atomic.Add(ref released[0], 0) == 0) { }
                output[index] = input[index.X ^ 7];
            }
        }
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
        var inputData = new int[Length];
        for (int i = 0; i < Length; i++)
            inputData[i] = 10 + i;

        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(Length);
        using var released = stream.Allocate1D<int>(1);
        released.MemSetToZero(stream);
        stream.Launch((Index1D)Length, index => Kernels.WarpBarrierDivergentKernel(
            index, input.View, output.View, released.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        for (int i = 0; i < 8; i++)
            Console.WriteLine(values[i]);
        int mismatches = 0;
        for (int i = 0; i < Length; i++)
        {
            if (values[i] != inputData[i ^ 7])
                ++mismatches;
        }
        Console.WriteLine(mismatches);
    }
}
