// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedVariableRefCalls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedVariableRefCalls
// Helpers that are not inlined return references to a single shared variable
// and to an element of a shared array. The kernels reset a group counter,
// count the threads of the group atomically through the returned reference,
// and pass it back into helpers by ref, also in a loop. Runs groups of several
// warps where the device allows.
// Expected output: one line with 0 mismatches per kernel

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int MaxGroupSize = 256;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int Counter() => ref Group.GetSharedMemory<int>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int Slot(int index) =>
        ref Group.GetSharedMemory<int>(MaxGroupSize)[index];

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AddTo(ref int target, int value) => Atomic.Add(ref target, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Load(ref int source) => source;

    public static void CounterCallKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        if (Group.IsFirstThread)
            Counter() = 0;
        Group.Barrier();
        Atomic.Add(ref Counter(), 1);
        Group.Barrier();
        output[(int)Grid.GlobalThreadIndex] = Counter();
    }

    public static void SlotCallKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        Slot(Group.Index) = global;
        Group.Barrier();
        output[global] = Slot((Group.Index + 1) % Group.Dimension);
    }

    public static void CallArgumentKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        ref int counter = ref Counter();
        if (Group.IsFirstThread)
            counter = 0;
        Group.Barrier();
        AddTo(ref counter, 2);
        for (int i = 0; i < 3; ++i)
            AddTo(ref Counter(), 1);
        Group.Barrier();
        output[(int)Grid.GlobalThreadIndex] = Load(ref Counter()) + counter;
    }
}

static class Program
{
    static int Neighbour(int index, int groupSize)
    {
        int lane = index % groupSize;
        return index - lane + (lane + 1) % groupSize;
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, Kernels.MaxGroupSize);
        const int GridSize = 3;
        int total = GridSize * groupSize;
        var config = new KernelConfig(GridSize, groupSize);
        using var output = stream.Allocate1D<int>(total);

        int Count(Func<int, int> expected)
        {
            stream.Synchronize();
            var values = output.GetAsArray1D();
            int mismatches = 0;
            for (int i = 0; i < total; ++i)
            {
                if (values[i] != expected(i))
                    ++mismatches;
            }
            return mismatches;
        }

        stream.Launch(in config, index => Kernels.CounterCallKernel(output.View));
        Console.WriteLine($"counter call mismatches {Count(i => groupSize)}");

        stream.Launch(in config, index => Kernels.SlotCallKernel(output.View));
        Console.WriteLine(
            $"slot call mismatches {Count(i => Neighbour(i, groupSize))}");

        stream.Launch(in config, index => Kernels.CallArgumentKernel(output.View));
        Console.WriteLine($"call argument mismatches {Count(i => groupSize * 10)}");
    }
}
