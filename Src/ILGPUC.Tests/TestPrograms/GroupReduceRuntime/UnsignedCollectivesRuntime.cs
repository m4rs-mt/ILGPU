// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedCollectivesRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: UnsignedCollectivesRuntime
// Group and warp reductions and scans with an unsigned minimum or maximum over uint
// and ulong values, as lambdas and as method groups of Math and XMath, in groups of
// several warps where the device allows. Every warp mixes values with the highest
// bit set, which are negative as signed values, with small ones, and the identities
// are 0 and MaxValue, which are 0 and -1 as signed values: a signed minimum or
// maximum gives other results.
// Expected output: one line "<collective> mismatches 0" per collective and type.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int Count = 16;

    public static void UIntCollectivesKernel(
        ArrayView1D<uint, Stride1D.Dense> input,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        uint value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, 0u, Math.Max);
        output[offset + 1] = Group.Reduce(value, uint.MaxValue, (a, b) => Math.Min(a, b));
        output[offset + 2] = Group.AllReduce(value, 0u, XMath.Max);
        output[offset + 3] = Group.AllReduce(value, uint.MaxValue, Math.Min);
        output[offset + 4] = Group.InclusiveScan(value, 0u, (a, b) => Math.Max(a, b));
        output[offset + 5] = Group.InclusiveScan(value, uint.MaxValue, XMath.Min);
        output[offset + 6] = Group.ExclusiveScan(value, 0u, Math.Max);
        output[offset + 7] = Group.ExclusiveScan(value, uint.MaxValue, Math.Min);
        output[offset + 8] = Warp.Reduce(value, (a, b) => Math.Max(a, b));
        output[offset + 9] = Warp.Reduce(value, Math.Min);
        output[offset + 10] = Warp.AllReduce(value, XMath.Max);
        output[offset + 11] = Warp.AllReduce(value, (a, b) => XMath.Min(a, b));
        output[offset + 12] = Warp.InclusiveScan(value, Math.Max);
        output[offset + 13] = Warp.InclusiveScan(value, (a, b) => Math.Min(a, b));
        output[offset + 14] = Warp.ExclusiveScan(value, 0u, XMath.Max);
        output[offset + 15] = Warp.ExclusiveScan(value, uint.MaxValue, Math.Min);
    }

    public static void ULongCollectivesKernel(
        ArrayView1D<ulong, Stride1D.Dense> input,
        ArrayView1D<ulong, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        ulong value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, 0ul, XMath.Max);
        output[offset + 1] = Group.Reduce(value, ulong.MaxValue, Math.Min);
        output[offset + 2] = Group.AllReduce(value, 0ul, (a, b) => Math.Max(a, b));
        output[offset + 3] = Group.AllReduce(value, ulong.MaxValue, XMath.Min);
        output[offset + 4] = Group.InclusiveScan(value, 0ul, Math.Max);
        output[offset + 5] = Group.InclusiveScan(value, ulong.MaxValue, Math.Min);
        output[offset + 6] = Group.ExclusiveScan(value, 0ul, XMath.Max);
        output[offset + 7] = Group.ExclusiveScan(
            value,
            ulong.MaxValue,
            (a, b) => Math.Min(a, b));
        output[offset + 8] = Warp.Reduce(value, Math.Max);
        output[offset + 9] = Warp.Reduce(value, XMath.Min);
        output[offset + 10] = Warp.AllReduce(value, (a, b) => Math.Max(a, b));
        output[offset + 11] = Warp.AllReduce(value, Math.Min);
        output[offset + 12] = Warp.InclusiveScan(value, XMath.Max);
        output[offset + 13] = Warp.InclusiveScan(value, Math.Min);
        output[offset + 14] = Warp.ExclusiveScan(value, 0ul, Math.Max);
        output[offset + 15] = Warp.ExclusiveScan(
            value,
            ulong.MaxValue,
            (a, b) => XMath.Min(a, b));
    }
}

static class Program
{
    static readonly string[] Names =
    [
        "group reduce max",
        "group reduce min",
        "group all reduce max",
        "group all reduce min",
        "group inclusive scan max",
        "group inclusive scan min",
        "group exclusive scan max",
        "group exclusive scan min",
        "warp reduce max",
        "warp reduce min",
        "warp all reduce max",
        "warp all reduce min",
        "warp inclusive scan max",
        "warp inclusive scan min",
        "warp exclusive scan max",
        "warp exclusive scan min",
    ];

    /// <summary>
    /// Returns the minimum and maximum of the values in [start, end) and of the
    /// values in [start, index) and [start, index].
    /// </summary>
    static (ulong Min, ulong Max, ulong PrefixMin, ulong PrefixMax) Scan(
        ulong[] values,
        int start,
        int end,
        int index,
        ulong maxValue)
    {
        ulong min = maxValue, max = 0, prefixMin = maxValue, prefixMax = 0;
        for (int j = start; j < end; j++)
        {
            min = Math.Min(min, values[j]);
            max = Math.Max(max, values[j]);
            if (j < index)
            {
                prefixMin = Math.Min(prefixMin, values[j]);
                prefixMax = Math.Max(prefixMax, values[j]);
            }
        }
        return (min, max, prefixMin, prefixMax);
    }

    /// <summary>
    /// Returns the expected result of each collective of each thread, computed
    /// over the given values with the given maximum of the type.
    /// </summary>
    static ulong[] Expect(ulong[] values, int groupSize, int warpSize, ulong maxValue)
    {
        var expected = new ulong[values.Length * Kernels.Count];
        for (int i = 0; i < values.Length; i++)
        {
            int group = i / groupSize * groupSize;
            int warp = group + (i - group) / warpSize * warpSize;
            int warpEnd = Math.Min(warp + warpSize, group + groupSize);
            var g = Scan(values, group, group + groupSize, i, maxValue);
            var w = Scan(values, warp, warpEnd, i, maxValue);
            ulong value = values[i];

            int offset = i * Kernels.Count;
            expected[offset] = g.Max;
            expected[offset + 1] = g.Min;
            expected[offset + 2] = g.Max;
            expected[offset + 3] = g.Min;
            expected[offset + 4] = Math.Max(g.PrefixMax, value);
            expected[offset + 5] = Math.Min(g.PrefixMin, value);
            expected[offset + 6] = g.PrefixMax;
            expected[offset + 7] = g.PrefixMin;
            expected[offset + 8] = w.Max;
            expected[offset + 9] = w.Min;
            expected[offset + 10] = w.Max;
            expected[offset + 11] = w.Min;
            expected[offset + 12] = Math.Max(w.PrefixMax, value);
            expected[offset + 13] = Math.Min(w.PrefixMin, value);
            expected[offset + 14] = w.PrefixMax;
            expected[offset + 15] = w.PrefixMin;
        }
        return expected;
    }

    static void Report(
        string type,
        ulong[] actual,
        ulong[] expected,
        int groupSize,
        int warpSize)
    {
        for (int k = 0; k < Kernels.Count; k++)
        {
            int mismatches = 0;
            string first = string.Empty;
            for (int i = 0; i < actual.Length / Kernels.Count; i++)
            {
                // A group reduction is only valid in the first thread of a group,
                // a warp reduction only in the first lane of a warp
                if (k < 2 && i % groupSize != 0)
                    continue;
                if (k is 8 or 9 && i % groupSize % warpSize != 0)
                    continue;
                int offset = i * Kernels.Count + k;
                if (actual[offset] == expected[offset])
                    continue;
                if (mismatches++ == 0)
                {
                    first = $" (thread {i}: 0x{actual[offset]:X}, " +
                        $"expected 0x{expected[offset]:X})";
                }
            }
            Console.WriteLine($"{type} {Names[k]} mismatches {mismatches}{first}");
        }
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int warpSize = accelerator.WarpSize;
        int groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 4 * warpSize);
        const int GridSize = 3;
        int total = GridSize * groupSize;
        var config = new KernelConfig(GridSize, groupSize);

        // Every third value has its highest bit set; the others are small, and
        // some are 0 and MaxValue, the identities of the maximum and minimum
        var uintValues = new uint[total];
        var ulongValues = new ulong[total];
        for (int i = 0; i < total; i++)
        {
            uint low = (uint)((i * 37 + i / 5) % 1009) + 1;
            uintValues[i] = (i % 3) switch
            {
                0 => 0x80000000u + low * 4099u,
                _ when i % 11 == 1 => 0u,
                _ when i % 13 == 2 => uint.MaxValue,
                _ => low,
            };
            ulongValues[i] = (i % 3) switch
            {
                0 => 0x8000000000000000ul + low * 0x100000001ul,
                _ when i % 11 == 1 => 0ul,
                _ when i % 13 == 2 => ulong.MaxValue,
                _ => low,
            };
        }

        using (var input = stream.Allocate1D(uintValues))
        using (var output = stream.Allocate1D<uint>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.UIntCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            Report(
                "uint",
                Array.ConvertAll(output.GetAsArray1D(), v => (ulong)v),
                Expect(
                    Array.ConvertAll(uintValues, v => (ulong)v),
                    groupSize,
                    warpSize,
                    uint.MaxValue),
                groupSize,
                warpSize);
        }

        using (var input = stream.Allocate1D(ulongValues))
        using (var output = stream.Allocate1D<ulong>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.ULongCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            Report(
                "ulong",
                output.GetAsArray1D(),
                Expect(ulongValues, groupSize, warpSize, ulong.MaxValue),
                groupSize,
                warpSize);
        }
    }
}
