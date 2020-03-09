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

namespace SimpleMath
{
    class Program
    {
        /// <summary>
        /// A simple 1D kernel using math functions.
        /// The <see cref="IntrinsicMath"/> class contains intrinsic math functions that -
        /// in contrast to the default .Net Math class - work on both floats and doubles. Note that
        /// the /// <see cref="IntrinsicMath"/> class is supported on all accelerators.
        /// The CompileUnitFlags.FastMath flag can be used during the creation of the compile unit
        /// to enable fast math intrinsics.
        /// Note that the full power of math functions on all accelerators is available via the
        /// Algorithms library (see ILGPU.Algorithms.Math sample).
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A uniform constant.</param>
        static void MathKernel(
            Index1 index,                    // The global thread index (1D in this case)
            ArrayView<float> singleView,    // A view of floats to store float results from GPUMath
            ArrayView<double> doubleView,   // A view of doubles to store double results from GPUMath
            ArrayView<double> doubleView2)  // A view of doubles to store double results from .Net Math
        {
            // Note the different returns type of GPUMath.Sqrt and Math.Sqrt.
            singleView[index] = IntrinsicMath.Abs(index);
            doubleView[index] = IntrinsicMath.Clamp((double)(int)index, 0.0, 12.0);

            // Note that use can safely use functions from the Math class as long as they have a counterpart
            // in the IntrinsicMath class.
            doubleView2[index] = Math.Min(0.2, index);
        }

        /// <summary>
        /// Launches a simple math kernel.
        /// </summary>
        static void Main()
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
                        var kernel = accelerator.LoadAutoGroupedStreamKernel<
                            Index1, ArrayView<float>, ArrayView<double>, ArrayView<double>>(MathKernel);

                        var buffer = accelerator.Allocate<float>(128);
                        var buffer2 = accelerator.Allocate<double>(128);
                        var buffer3 = accelerator.Allocate<double>(128);

                        // Launch buffer.Length many threads
                        kernel(buffer.Length, buffer.View, buffer2.View, buffer3.View);

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
                    }
                }
            }
        }
    }
}
