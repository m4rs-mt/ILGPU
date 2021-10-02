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
using ILGPU.Runtime.Cuda;
using System;

namespace MonitorProgress
{
    class Program
    {
        public const int NumThreads = 1024;
        public const int NumIterations = 10_000;
        public const int MaxProgress = NumThreads * NumIterations;

        public static void MyKernel(Index1D index, ArrayView<int> progress)
        {
            for (var i = 0; i < NumIterations; i++)
            {
                Atomic.Add(ref progress[0], 1);
                MemoryFence.SystemLevel();
            }
        }

        /// <summary>
        /// Demonstrates monitoring progress of a Cuda kernel, based on:
        /// https://stackoverflow.com/questions/20345702/how-can-i-check-the-progress-of-matrix-multiplication/20381924#20381924
        /// </summary>
        static void Main()
        {
            using var context = Context.Create(builder => builder.Cuda().Profiling());

            foreach (var device in context.GetCudaDevices())
            {
                using var accelerator = device.CreateCudaAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");
                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(MyKernel);

                // Create a host buffer to store the progress value.
                using var progress = new CudaProgress<int>(accelerator);

                // Run the kernel, and apply workaround for issue on Windows with WDDM.
                // https://stackoverflow.com/questions/20345702/how-can-i-check-the-progress-of-matrix-multiplication/20381924#comment55308772_20381924
                // https://stackoverflow.com/questions/33455396/cuda-mapped-memory-device-host-writes-are-not-visible-on-host
                using var marker = accelerator.AddProfilingMarker();

                kernel(NumThreads, progress.View);

                marker.Synchronize();

                // Monitor the progress of the kernel.
                int progressValue;
                do
                {
                    progressValue = progress.Value;
                    Console.WriteLine($"{progressValue} of {MaxProgress}");
                } while (progressValue < MaxProgress);

                // Wait for the kernel to finish before the accelerator is disposed
                // at the end of this block.
                accelerator.Synchronize();
            }
        }
    }
}
