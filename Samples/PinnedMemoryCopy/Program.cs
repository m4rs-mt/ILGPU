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


// disable: max_line_length
using System;
using System.Linq;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

namespace PinnedMemoryCopy;

static class Program
{
    static void PerformPinnedCopyUsingGCHandle(Accelerator accelerator, int dataSize)
    {
        var array = new int[dataSize];
        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        try
        {
            using var bufferOnGPU = accelerator.DefaultStream.Allocate1D<int>(array.Length);
            var stream = accelerator.DefaultStream;

            using var scope = accelerator.CreatePageLockFromPinned(array);
            bufferOnGPU.View.CopyFrom(stream, scope.ArrayView);

            stream.Synchronize();
        }
        finally
        {
            handle.Free();
        }
    }

    static void PerformPinnedCopyUsingGCAllocateArray(Accelerator accelerator, int dataSize)
    {
        var array = GC.AllocateArray<int>(dataSize, pinned: true);

        using var bufferOnGPU = accelerator.DefaultStream.Allocate1D<int>(array.Length);
        var stream = accelerator.DefaultStream;

        using var scope = accelerator.CreatePageLockFromPinned(array);
        bufferOnGPU.View.CopyFrom(stream, scope.ArrayView);

        stream.Synchronize();
    }

    static void PerformPinnedCopyUsingAllocatePageLockedArray(
        Accelerator accelerator,
        int dataSize)
    {
        using var array = accelerator.AllocatePageLocked1D<int>(dataSize);

        using var bufferOnGPU = accelerator.DefaultStream.Allocate1D<int>(array.Length);
        var stream = accelerator.DefaultStream;

        bufferOnGPU.View.CopyFrom(stream, array.ArrayView);

        stream.Synchronize();

        // Retrieve results into existing page locked array
        bufferOnGPU.View.CopyTo(stream, array.ArrayView);

        // Retrieve results into a new array
        var result1 = bufferOnGPU.GetAsArray1D();

        // Retrieve results into a new page locked array
        var result2 = bufferOnGPU.View.GetAsPageLocked1D();
    }

    static void Main()
    {
        const int DataSize = 1024;

        using (var context = Context.CreateDefault())
        {
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

            PerformPinnedCopyUsingGCHandle(accelerator, DataSize);
            PerformPinnedCopyUsingGCAllocateArray(accelerator, DataSize);
            PerformPinnedCopyUsingAllocatePageLockedArray(accelerator, DataSize);
        }

        // Enable automatic page locking
        using (var context = Context.Create(builder =>
            builder.Default().PageLocking(PageLockingMode.Auto)))
        {
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
            Console.WriteLine($"Using {accelerator} (Automatic Page Locking)");

            PerformPinnedCopyUsingAllocatePageLockedArray(accelerator, DataSize);
        }

        Console.WriteLine("Done.");
    }
}
