// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Linq;
using ILGPU;
using ILGPU.Initialization;
using ILGPU.Runtime;

namespace AlgorithmsSequence;

struct CustomStruct
{
    public long First;
    public long Second;

    public override string ToString() =>
        $"First: {First}, Second: {Second}";
}

static class Program
{
    static void Sequence(AcceleratorStream stream)
    {
        // Simple int sequence 0..63
        using (var buffer = stream.Allocate1D<int>(64))
        {
            stream.Sequence(buffer.View, i => (int)i);
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        // Custom struct sequence
        using (var buffer = stream.Allocate1D<CustomStruct>(64))
        {
            stream.Sequence(buffer.View, i => new CustomStruct
            {
                First = i,
                Second = 32 + i
            });
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"CustomData[{i}] = {data[i]}");
        }
    }

    static void RepeatedSequence(AcceleratorStream stream)
    {
        using var buffer = stream.Allocate1D<int>(64);

        // Repeated sequence with sequenceLength = 32 (repeats every 32 elements)
        stream.Sequence(buffer.View, i => (int)i, sequenceLength: 32);
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"RepeatedData[{i}] = {data[i]}");
    }

    static void BatchedSequence(AcceleratorStream stream)
    {
        using var buffer = stream.Allocate1D<int>(64);

        // Batched sequence with batch size 2
        // [0,0,1,1,2,2,3,3,...]
        stream.Sequence(buffer.View, i => (int)i, sequenceBatchLength: 2);
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"BatchedData[{i}] = {data[i]}");
    }

    static void RepeatedBatchedSequence(AcceleratorStream stream)
    {
        using var buffer = stream.Allocate1D<int>(64);

        // Repeated batched sequence
        stream.Sequence(
            buffer.View,
            i => (int)i,
            sequenceLength: 2,
            sequenceBatchLength: 4);
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"RepeatedBatchedData[{i}] = {data[i]}");
    }

    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        Sequence(stream);
        RepeatedSequence(stream);
        BatchedSequence(stream);
        RepeatedBatchedSequence(stream);
    }
}
