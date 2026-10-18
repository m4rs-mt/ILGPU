// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Initialization;
using ILGPU.Runtime;
using ILGPU.ScanReduce;

namespace Scan;

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.GetPreferredDevice(preferCPU: false);

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        const int N = 32;
        using var sourceBuffer = stream.Allocate1D<int>(N);
        stream.Initialize(sourceBuffer.View, 2);

        // Inclusive scan
        using (var targetBuffer = stream.Allocate1D<int>(N))
        {
            stream.InclusiveScan(
                sourceBuffer.View,
                targetBuffer.View,
                identity: 0,
                operation: (a, b) => a + b);
            stream.Synchronize();

            Console.WriteLine("Inclusive Scan:");
            var data = targetBuffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        // Exclusive scan
        using (var targetBuffer = stream.Allocate1D<int>(N))
        {
            stream.ExclusiveScan(
                sourceBuffer.View,
                targetBuffer.View,
                identity: 0,
                operation: (a, b) => a + b);
            stream.Synchronize();

            Console.WriteLine("Exclusive Scan:");
            var data = targetBuffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }
    }
}
