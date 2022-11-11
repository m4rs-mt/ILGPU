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
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using System;
using System.Runtime.CompilerServices;

namespace DynamicSharedMemory
{
    class Program
    {
        // NoInlining -> simulates a larger helper method that could not be inline for some reason
        // but leverages dynamically shared memory.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DynamicSharedMemoryHelper(int globalIndex, ArrayView<short> view)
        {
            // Get a dynamically allocated shared memory view with a custom element type.
            var dynamicMemory = SharedMemory.GetDynamic<short>();

            // Store data in shared memory
            dynamicMemory[Group.IdxX] = (short)Group.IdxX;
            Group.Barrier();

            // Read data from shared memory
            view[globalIndex] = dynamicMemory[Group.IdxX];
        }

        // The kernel
        public static void SharedMemKernel(ArrayView<int> view1, ArrayView<short> view2)
        {
            var globalIndex = Grid.GlobalIndex.X;

            // Allocate a statically known amount of shared memory
            // var staticMemory = SharedMemory.Allocate<int>(1024);

            // Get a dynamically allocated shared memory view.
            var dynamicMemory = SharedMemory.GetDynamic<int>();

            // Store data in shared memory
            dynamicMemory[Group.IdxX] = Group.IdxX;
            Group.Barrier();

            // Read data from shared memory
            view1[globalIndex] = dynamicMemory[Group.IdxX];

            // Call another function that uses dynamic shared memory
            DynamicSharedMemoryHelper(globalIndex, view2);
        }

        static void Main()
        {
            using var context = Context.Create(builder => builder.DefaultCPU().Cuda());

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Use LoadStreamKernel or LoadKernel to load explicitly grouped kernels
                var kernel = accelerator.LoadStreamKernel<ArrayView<int>, ArrayView<short>>(SharedMemKernel);
                var buffer = accelerator.Allocate1D<int>(accelerator.MaxNumThreadsPerGroup);
                var buffer2 = accelerator.Allocate1D<short>(accelerator.MaxNumThreadsPerGroup);

                // Use 'new KernelConfig(..., ..., ...)' to construct a new launch configuration
                // Hint: use the C# tuple features to convert a triple into a kernel config
                int groupSize = accelerator.MaxNumThreadsPerGroup;
                var config = SharedMemoryConfig.RequestDynamic<byte>(groupSize * sizeof(int));
                // alternatively:
                // var config = SharedMemoryConfig.RequestDynamic<int>(groupSize);
                // var config = SharedMemoryConfig.RequestDynamic<short>(groupSize * 2);
                kernel(
                    // GridSize, GroupSize, shared memory config
                    (1, groupSize, config),
                    buffer.View,
                    buffer2.View);

                var data = buffer.GetAsArray1D();
                var data2 = buffer2.GetAsArray1D();
                // Use data objects...
            }
        }
    }
}
