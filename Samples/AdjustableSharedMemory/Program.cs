// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace SharedMemory
{
    class Program
    {
        /// <summary>
        /// Specifies an abstract interface to support compile-time specialization.
        /// </summary>
        interface ISharedAllocationSize
        {
            /// <summary>
            /// A simple property implementation will be automatically inlined.
            /// </summary>
            int ArraySize { get; }
        }

        struct SharedArray32 : ISharedAllocationSize
        {
            /// <summary>
            /// Returns a 
            /// </summary>
            public int ArraySize => 32;
        }

        struct SharedArray64 : ISharedAllocationSize
        {
            public int ArraySize => 64;
        }

        /// <summary>
        /// Demonstrates the use of shared-memory variable referencing multiple elements.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="outputView">The view pointing to our memory buffer.</param>
        /// <param name="sharedArray">Implicit shared-memory parameter that is handled by the runtime.</param>
        static void SharedMemoryKernel<TSharedAllocationSize>(
            ArrayView<int> outputView)   // A view to a chunk of memory (1D in this case)
            where TSharedAllocationSize : struct, ISharedAllocationSize
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            // Declares a shared-memory array with a number elements of type int
            // that are resolved via the generic parameter.
            // of shared memory per group
            // Note that an allocation of an array view (currently) requires a compile-time known
            // constant array size.
            var sharedAllocationSize = new TSharedAllocationSize();
            var sharedArray = ILGPU.SharedMemory.Allocate<int>(sharedAllocationSize.ArraySize);

            outputView[globalIndex] = sharedArray.IntLength;
        }

        static void ExecuteSample<TSharedAllocationSize>(Context context)
            where TSharedAllocationSize : struct, ISharedAllocationSize
        {
            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                using var dataTarget = accelerator.Allocate1D<int>(accelerator.MaxNumThreadsPerGroup);
                // Load a specialized shared-memory kernel
                var sharedMemoryKernel = accelerator.LoadStreamKernel<
                    ArrayView<int>>(SharedMemoryKernel<TSharedAllocationSize>);
                dataTarget.MemSetToZero();

                // Note that shared memory cannot be accessed from the outside
                // and must be initialized by the kernel
                sharedMemoryKernel(
                    (1, accelerator.MaxNumThreadsPerGroup),
                    dataTarget.View);

                accelerator.Synchronize();

                Console.WriteLine("Shared-memory kernel");
                var target = dataTarget.GetAsArray1D();
                for (int i = 0, e = target.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {target[i]}");
            }
        }

        /// <summary>
        /// Launches a simple 1D kernel using shared memory.
        /// </summary>
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();
            ExecuteSample<SharedArray32>(context);
            ExecuteSample<SharedArray64>(context);
        }
    }
}
