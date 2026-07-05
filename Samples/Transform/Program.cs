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

namespace Transform;

public struct CustomStruct
{
    public int First { get; set; }
    public int Second { get; set; }

    public override string ToString() =>
        $"First: {First}, Second: {Second}";
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.GetPreferredDevice(preferCPU: false);

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        using var sourceBuffer = stream.Allocate1D<int>(64);
        stream.Initialize(sourceBuffer.View, 2);

        using var targetBuffer = stream.Allocate1D<CustomStruct>(64);

        // Transform int -> CustomStruct using a lambda
        stream.Transform(sourceBuffer.View, targetBuffer.View, value =>
            new CustomStruct
            {
                First = value,
                Second = value * value
            });
        stream.Synchronize();

        var data = targetBuffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");
    }
}
