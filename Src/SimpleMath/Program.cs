// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
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
using System.Reflection;

namespace SimpleMath
{
    class Program
    {
        /// <summary>
        /// A simple 1D kernel using math functions.
        /// The GPUMath class contains intrinsic math functions that -
        /// in contrast to the default .Net Math class -
        /// work on both floats and doubles.
        /// Note that both classes are supported on all
        /// accelerators. The CompileUnitFlags.FastMath flag can be used during the creation of the
        /// compile unit to enable fast math intrinsics.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A uniform constant.</param>
        static void MathKernel(
            Index index,                    // The global thread index (1D in this case)
            ArrayView<float> singleView,    // A view of floats to store float results from GPUMath
            ArrayView<double> doubleView,   // A view of doubles to store double results from GPUMath
            ArrayView<double> doubleView2)  // A view of doubles to store double results from .Net Math
        {
            // Note the different returns type of GPUMath.Sqrt and Math.Sqrt.
            singleView[index] = GPUMath.Sqrt(index);
            doubleView[index] = GPUMath.Sqrt((double)index);
            doubleView2[index] = Math.Sqrt(index);
        }

        /// <summary>
        /// Launches a simple math kernel.
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        using (var loader = new SimpleKernel.SampleKernelLoader())
                        {
                            loader.CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(MathKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                kernel =>
                                {
                                    var buffer = accelerator.Allocate<float>(128);
                                    var buffer2 = accelerator.Allocate<double>(128);
                                    var buffer3 = accelerator.Allocate<double>(128);

                                    // Launch buffer.Length many threads
                                    kernel.Launch(buffer.Length, buffer.View, buffer2.View, buffer3.View);

                                    // Wait for the kernel to finish...
                                    accelerator.Synchronize();

                                    // Resolve and verify data
                                    var data = buffer.GetAsArray();
                                    var data2 = buffer2.GetAsArray();
                                    var data3 = buffer3.GetAsArray();
                                    for (int i = 0, e = data.Length; i < e; ++i)
                                        Console.WriteLine($"Math results: {data[i]} (float) {data2[i]} (double [GPUMath]) {data3[i]} (double [.Net Math])");

                                    buffer.Dispose();
                                    buffer2.Dispose();
                                    buffer3.Dispose();
                                });
                        }
                    }
                }
            }
        }
    }
}
