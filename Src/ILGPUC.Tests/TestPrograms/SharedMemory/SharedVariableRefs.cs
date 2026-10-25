// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedVariableRefs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedVariableRefs
// Inlined helpers return references to a single shared variable and to an
// element of a shared array. The kernels reset a group counter, count the
// threads of the group atomically through the returned reference, pass a
// reference to the shared variable into helpers by ref, load and store
// through it, and add to it in a loop. Runs groups of several warps where the
// device allows.
// Expected output: one line with 0 mismatches per kernel

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int MaxGroupSize = 256;

    static ref int Counter() => ref Group.GetSharedMemory<int>();

    static ref int Slot(int index) =>
        ref Group.GetSharedMemory<int>(MaxGroupSize)[index];

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AddTo(ref int target, int value) => Atomic.Add(ref target, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Store(ref int target, int value) => target = value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Load(ref int source) => source;

    public static void CounterKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        if (Group.IsFirstThread)
            Counter() = 0;
        Group.Barrier();
        Atomic.Add(ref Counter(), 1);
        Group.Barrier();
        output[(int)Grid.GlobalThreadIndex] = Counter();
    }

    public static void SlotKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        Slot(Group.Index) = global;
        Group.Barrier();
        output[global] = Slot((Group.Index + 1) % Group.Dimension);
    }

    public static void RefArgumentKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        ref int counter = ref Group.GetSharedMemory<int>();
        if (Group.IsFirstThread)
            Store(ref counter, 0);
        Group.Barrier();
        AddTo(ref counter, 2);
        Atomic.Add(ref counter, 3);
        Group.Barrier();
        output[(int)Grid.GlobalThreadIndex] = Load(ref counter) + counter;
    }

    public static void LoopKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        ref int counter = ref Counter();
        if (Group.IsFirstThread)
            counter = 0;
        Group.Barrier();
        for (int i = 1; i <= 4; ++i)
            Atomic.Add(ref Counter(), i);
        Group.Barrier();
        int value = 0;
        for (int i = 0; i < 2; ++i)
            value += counter;
        output[(int)Grid.GlobalThreadIndex] = value;
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

        stream.Launch(in config, index => Kernels.CounterKernel(output.View));
        Console.WriteLine($"counter mismatches {Count(i => groupSize)}");

        stream.Launch(in config, index => Kernels.SlotKernel(output.View));
        Console.WriteLine(
            $"slot mismatches {Count(i => Neighbour(i, groupSize))}");

        stream.Launch(in config, index => Kernels.RefArgumentKernel(output.View));
        Console.WriteLine($"ref argument mismatches {Count(i => groupSize * 10)}");

        stream.Launch(in config, index => Kernels.LoopKernel(output.View));
        Console.WriteLine($"loop mismatches {Count(i => groupSize * 20)}");
    }
}
