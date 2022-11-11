// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace SimpleAtomics
{
    /// <summary>
    /// CAUTION: This sample might not run on some GPUs due to missing support for atomic functions.
    /// </summary>
    class Program
    {
        /// <summary>
        /// A simple 1D kernel using basic atomic functions.
        /// The second parameter (<paramref name="dataView"/>) represents the target
        /// view for all atomic operations.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A uniform constant.</param>
        static void AtomicOperationKernel(
            Index1D index,             // The global thread index (1D in this case)
            ArrayView<int> dataView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A sample uniform constant
        {
            // dataView[0] += constant
            Atomic.Add(ref dataView[0], constant);

            // dataView[1] = Max(dataView[1], constant)
            Atomic.Max(ref dataView[1], constant);

            // dataView[2] = Min(dataView[2], constant)
            Atomic.Min(ref dataView[2], constant);

            // dataView[3] = Min(dataView[3], constant)
            Atomic.And(ref dataView[3], constant);

            // dataView[4] = Min(dataView[4], constant)
            Atomic.Or(ref dataView[4], constant);

            // dataView[6] = Min(dataView[5], constant)
            Atomic.Xor(ref dataView[5], constant);
        }

        /// <summary>
        /// Launches a simple 1D kernel.
        /// </summary>
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var kernel = accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<int>, int>(AtomicOperationKernel);

                // Initialize buffer to zero
                using var buffer = accelerator.Allocate1D<int>(7);
                buffer.MemSetToZero();

                // Launch buffer.Length many threads and pass a view to buffer
                kernel(1024, buffer.View, 4);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");
            }
        }
    }
}
