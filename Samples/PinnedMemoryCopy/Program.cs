// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Runtime.InteropServices;

namespace PinnedMemoryCopy
{
    class Program
    {
        /// <summary>
        /// Uses GCHandle to allocate pinned chunks of memory in CPU host memory.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="dataSize">The number of elements to copy.</param>
        static void PerformPinnedCopyUsingGCHandle(Accelerator accelerator, int dataSize)
        {
            var array = new int[dataSize];
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                // Allocate buffer on this device
                using var bufferOnGPU = accelerator.Allocate1D<int>(array.Length);
                var stream = accelerator.DefaultStream;

                // Page locked buffers enable async memory transfers
                using var scope = accelerator.CreatePageLockFromPinned(array);
                bufferOnGPU.View.CopyFrom(stream, scope.ArrayView);

                //
                // Perform other operations...
                //

                // Wait for the copy operation to finish
                stream.Synchronize();
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Uses System.GC.AllocateArray to allocate pinned chunks of memory in CPU host memory.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="dataSize">The number of elements to copy.</param>
        static void PerformPinnedCopyUsingGCAllocateArray(Accelerator accelerator, int dataSize)
        {
            var array = GC.AllocateArray<int>(dataSize, pinned: true);

            // Allocate buffer on this device
            using var bufferOnGPU = accelerator.Allocate1D<int>(array.Length);
            var stream = accelerator.DefaultStream;

            // Page locked buffers enable async memory transfers
            using var scope = accelerator.CreatePageLockFromPinned(array);
            bufferOnGPU.View.CopyFrom(stream, scope.ArrayView);

            //
            // Perform other operations...
            //

            // Wait for the copy operation to finish
            stream.Synchronize();
        }

        /// <summary>
        /// Uses Accelerator.AllocatePageLockedArray1D to allocate pinned chunks of memory in CPU host memory.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="dataSize">The number of elements to copy.</param>
        static void PerformPinnedCopyUsingAllocatePageLockedArray(Accelerator accelerator, int dataSize)
        {
            using var array = accelerator.AllocatePageLocked1D<int>(dataSize);

            // Allocate buffer on this device
            using var bufferOnGPU = accelerator.Allocate1D<int>(array.Length);
            var stream = accelerator.DefaultStream;

            bufferOnGPU.View.CopyFrom(stream, array.ArrayView);

            //
            // Perform other operations...
            //

            // Wait for the copy operation to finish
            stream.Synchronize();

            // Retrieve the results into an existing page locked array
            bufferOnGPU.View.CopyTo(stream, array.ArrayView);

            // Retrieve the results into a new array
            // Rely on disabled (default) or automatic page locking behavior
            var result1 = bufferOnGPU.GetAsArray1D();

            // Explicitly retrieve the results into a new page locked array
            var result2 = bufferOnGPU.View.GetAsPageLocked1D();
        }

        /// <summary>
        /// Demonstrates async copy operations using the <see cref="CPUAccelerator"/> class to allocate
        /// pinned CPU memory.
        /// </summary>
        static void Main()
        {
            const int DataSize = 1024;

            using (var context = Context.CreateDefault())
            {
                // For each available device...
                foreach (var device in context)
                {
                    // Create accelerator for the given device
                    using var accelerator = device.CreateAccelerator(context);
                    Console.WriteLine($"Performing operations on {accelerator}");

                    PerformPinnedCopyUsingGCHandle(accelerator, DataSize);
                    PerformPinnedCopyUsingGCAllocateArray(accelerator, DataSize);
                    PerformPinnedCopyUsingAllocatePageLockedArray(accelerator, DataSize);
                }
            }

            // Enable automatic page locking
            using (var context = Context.Create(builder => builder.Default().PageLocking(PageLockingMode.Auto)))
            {
                // For each available device...
                foreach (var device in context)
                {
                    // Create accelerator for the given device
                    using var accelerator = device.CreateAccelerator(context);
                    Console.WriteLine($"Performing operations on {accelerator} (Automatic Page Locking)");

                    PerformPinnedCopyUsingAllocatePageLockedArray(accelerator, DataSize);
                }
            }
        }
    }
}
