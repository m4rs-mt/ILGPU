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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleConstants;

static class Kernels
{
    public const int ConstantValue = 1;

    [SuppressMessage(
        "Performance",
        "CA1802:Use literals where appropriate",
        Justification = "Testing readonly value")]
    public static readonly int ReadOnlyValue = 2;

    public static void ConstantKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dataView)
    {
        dataView[index] = ConstantValue;
    }

    public static void StaticFieldAccessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dataView)
    {
        dataView[index] = ReadOnlyValue;
    }
}

static class Program
{
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

        const int Length = 1024;

        // Launch ConstantKernel — uses a const field
        {
            using var buffer = stream.Allocate1D<int>(Length);
            stream.Launch(
                (Index1D)Length,
                index => Kernels.ConstantKernel(index, buffer.View));
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Debug.Assert(data[i] == Kernels.ConstantValue);
        }

        // Launch StaticFieldAccessKernel — uses a static readonly field
        {
            using var buffer = stream.Allocate1D<int>(Length);
            stream.Launch(
                (Index1D)Length,
                index => Kernels.StaticFieldAccessKernel(index, buffer.View));
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Debug.Assert(data[i] == Kernels.ReadOnlyValue);
        }

        Console.WriteLine("Done.");
    }
}
