// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SmallIntCollectivesRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SmallIntCollectivesRuntime
// Group and warp reductions and scans with XMath.Max and XMath.Min over short and
// sbyte values, which are computed in 32 bits, with negative values and the
// negative identity MinValue, in groups of several warps where the device allows.
// The same unsigned reductions and scans over ushort and byte values, half of
// them with the highest bit set, with Math.Max and Math.Min and the identities 0
// and MaxValue, which are computed in 32 bits after a zero extension.
// Expected output: one line "<collective> mismatches 0" per collective and type.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int Count = 9;

    public static void ShortCollectivesKernel(
        ArrayView1D<short, Stride1D.Dense> input,
        ArrayView1D<short, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        short value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, short.MinValue, XMath.Max);
        output[offset + 1] = Group.AllReduce(value, short.MinValue, XMath.Max);
        output[offset + 2] = Group.AllReduce(value, short.MaxValue, XMath.Min);
        output[offset + 3] = Group.InclusiveScan(value, short.MinValue, XMath.Max);
        output[offset + 4] = Group.ExclusiveScan(value, short.MinValue, XMath.Max);
        output[offset + 5] = Group.ExclusiveScan(value, short.MaxValue, XMath.Min);
        output[offset + 6] = Warp.ExclusiveScan(value, short.MinValue, XMath.Max);
        output[offset + 7] = Warp.AllReduce(value, XMath.Max);
        output[offset + 8] = Warp.InclusiveScan(value, XMath.Min);
    }

    public static void SByteCollectivesKernel(
        ArrayView1D<sbyte, Stride1D.Dense> input,
        ArrayView1D<sbyte, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        sbyte value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, sbyte.MinValue, XMath.Max);
        output[offset + 1] = Group.AllReduce(value, sbyte.MinValue, XMath.Max);
        output[offset + 2] = Group.AllReduce(value, sbyte.MaxValue, XMath.Min);
        output[offset + 3] = Group.InclusiveScan(value, sbyte.MinValue, XMath.Max);
        output[offset + 4] = Group.ExclusiveScan(value, sbyte.MinValue, XMath.Max);
        output[offset + 5] = Group.ExclusiveScan(value, sbyte.MaxValue, XMath.Min);
        output[offset + 6] = Warp.ExclusiveScan(value, sbyte.MinValue, XMath.Max);
        output[offset + 7] = Warp.AllReduce(value, XMath.Max);
        output[offset + 8] = Warp.InclusiveScan(value, XMath.Min);
    }

    public static void UShortCollectivesKernel(
        ArrayView1D<ushort, Stride1D.Dense> input,
        ArrayView1D<ushort, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        ushort value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, ushort.MinValue, Math.Max);
        output[offset + 1] = Group.AllReduce(value, ushort.MinValue, Math.Max);
        output[offset + 2] = Group.AllReduce(value, ushort.MaxValue, Math.Min);
        output[offset + 3] = Group.InclusiveScan(value, ushort.MinValue, Math.Max);
        output[offset + 4] = Group.ExclusiveScan(value, ushort.MinValue, Math.Max);
        output[offset + 5] = Group.ExclusiveScan(value, ushort.MaxValue, Math.Min);
        output[offset + 6] = Warp.ExclusiveScan(value, ushort.MinValue, Math.Max);
        output[offset + 7] = Warp.AllReduce(value, (a, b) => Math.Max(a, b));
        output[offset + 8] = Warp.InclusiveScan(value, (a, b) => Math.Min(a, b));
    }

    public static void ByteCollectivesKernel(
        ArrayView1D<byte, Stride1D.Dense> input,
        ArrayView1D<byte, Stride1D.Dense> output)
    {
        int index = (int)Grid.GlobalThreadIndex;
        byte value = input[index];
        int offset = index * Count;
        output[offset] = Group.Reduce(value, byte.MinValue, (a, b) => Math.Max(a, b));
        output[offset + 1] = Group.AllReduce(value, byte.MinValue, XMath.Max);
        output[offset + 2] = Group.AllReduce(value, byte.MaxValue, XMath.Min);
        output[offset + 3] = Group.InclusiveScan(value, byte.MinValue, Math.Max);
        output[offset + 4] = Group.ExclusiveScan(value, byte.MinValue, Math.Max);
        output[offset + 5] = Group.ExclusiveScan(value, byte.MaxValue, Math.Min);
        output[offset + 6] = Warp.ExclusiveScan(value, byte.MinValue, XMath.Max);
        output[offset + 7] = Warp.AllReduce(value, Math.Max);
        output[offset + 8] = Warp.InclusiveScan(value, Math.Min);
    }
}

static class Program
{
    static readonly string[] Names =
    [
        "group reduce max",
        "group all reduce max",
        "group all reduce min",
        "group inclusive scan max",
        "group exclusive scan max",
        "group exclusive scan min",
        "warp exclusive scan max",
        "warp all reduce max",
        "warp inclusive scan min",
    ];

    /// <summary>
    /// Returns the expected result of each collective of each thread, computed
    /// over the given values with the given minimum and maximum of the type.
    /// </summary>
    static long[] Expect(long[] values, int groupSize, int warpSize, long min, long max)
    {
        var expected = new long[values.Length * Kernels.Count];
        for (int i = 0; i < values.Length; i++)
        {
            int group = i / groupSize * groupSize;
            int warp = group + (i - group) / warpSize * warpSize;
            int warpEnd = Math.Min(warp + warpSize, group + groupSize);
            long groupMax = min, groupMin = max, prefixMax = min, prefixMin = max;
            for (int j = group; j < group + groupSize; j++)
            {
                groupMax = Math.Max(groupMax, values[j]);
                groupMin = Math.Min(groupMin, values[j]);
                if (j < i)
                {
                    prefixMax = Math.Max(prefixMax, values[j]);
                    prefixMin = Math.Min(prefixMin, values[j]);
                }
            }
            long warpPrefixMax = min, warpMax = min, warpPrefixMin = max;
            for (int j = warp; j < warpEnd; j++)
            {
                warpMax = Math.Max(warpMax, values[j]);
                if (j < i)
                    warpPrefixMax = Math.Max(warpPrefixMax, values[j]);
                if (j <= i)
                    warpPrefixMin = Math.Min(warpPrefixMin, values[j]);
            }

            int offset = i * Kernels.Count;
            expected[offset] = groupMax;
            expected[offset + 1] = groupMax;
            expected[offset + 2] = groupMin;
            expected[offset + 3] = Math.Max(prefixMax, values[i]);
            expected[offset + 4] = prefixMax;
            expected[offset + 5] = prefixMin;
            expected[offset + 6] = warpPrefixMax;
            expected[offset + 7] = warpMax;
            expected[offset + 8] = warpPrefixMin;
        }
        return expected;
    }

    static void Report(
        string type,
        long[] actual,
        long[] expected,
        int groupSize)
    {
        for (int k = 0; k < Kernels.Count; k++)
        {
            int mismatches = 0;
            string first = string.Empty;
            for (int i = 0; i < actual.Length / Kernels.Count; i++)
            {
                // A group reduction is only valid in the first thread of a group
                if (k == 0 && i % groupSize != 0)
                    continue;
                int offset = i * Kernels.Count + k;
                if (actual[offset] == expected[offset])
                    continue;
                if (mismatches++ == 0)
                {
                    first = $" (thread {i}: {actual[offset]}, " +
                        $"expected {expected[offset]})";
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

        // Negative values only, so that a wrongly widened identity wins a maximum
        var shortValues = new short[total];
        var sbyteValues = new sbyte[total];
        var values = new long[total];
        for (int i = 0; i < total; i++)
        {
            values[i] = -1 - (i * 37 + i / 5) % 113;
            shortValues[i] = (short)(values[i] * 251);
            sbyteValues[i] = (sbyte)values[i];
        }

        using (var input = stream.Allocate1D(shortValues))
        using (var output = stream.Allocate1D<short>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.ShortCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            var shortInputs = Array.ConvertAll(shortValues, v => (long)v);
            Report(
                "short",
                Array.ConvertAll(output.GetAsArray1D(), v => (long)v),
                Expect(shortInputs, groupSize, warpSize, short.MinValue, short.MaxValue),
                groupSize);
        }

        using (var input = stream.Allocate1D(sbyteValues))
        using (var output = stream.Allocate1D<sbyte>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.SByteCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            Report(
                "sbyte",
                Array.ConvertAll(output.GetAsArray1D(), v => (long)v),
                Expect(values, groupSize, warpSize, sbyte.MinValue, sbyte.MaxValue),
                groupSize);
        }

        // Every other value has its highest bit set, so that it is negative as a
        // signed value, and the others are small
        var ushortValues = new ushort[total];
        var byteValues = new byte[total];
        var unsignedValues = new long[total];
        for (int i = 0; i < total; i++)
        {
            unsignedValues[i] = (i * 37 + i / 5) % 113 + (i % 2 == 0 ? 0x80 : 1);
            ushortValues[i] = (ushort)(unsignedValues[i] * 257);
            byteValues[i] = (byte)unsignedValues[i];
        }

        using (var input = stream.Allocate1D(ushortValues))
        using (var output = stream.Allocate1D<ushort>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.UShortCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            Report(
                "ushort",
                Array.ConvertAll(output.GetAsArray1D(), v => (long)v),
                Expect(
                    Array.ConvertAll(ushortValues, v => (long)v),
                    groupSize,
                    warpSize,
                    ushort.MinValue,
                    ushort.MaxValue),
                groupSize);
        }

        using (var input = stream.Allocate1D(byteValues))
        using (var output = stream.Allocate1D<byte>(total * Kernels.Count))
        {
            stream.Launch(in config, index =>
                Kernels.ByteCollectivesKernel(input.View, output.View));
            stream.Synchronize();
            Report(
                "byte",
                Array.ConvertAll(output.GetAsArray1D(), v => (long)v),
                Expect(unsignedValues, groupSize, warpSize, byte.MinValue, byte.MaxValue),
                groupSize);
        }
    }
}
