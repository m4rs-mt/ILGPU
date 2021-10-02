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
using ILGPU.Runtime.OpenCL;
using System;
using System.Diagnostics;
using System.Linq;

namespace SimpleDebugAssert
{
    class Program
    {
        /// <summary>
        /// Example of using Debug.Assert to help debugging.
        /// </summary>
        static void DebugAssertKernel(Index1D index, ArrayView<int> dataView)
        {
            /// NB: Only <see cref="Debug.Assert(bool)"/> and
            /// <see cref="Debug.Assert(bool, string)"/> are currently supported.
            Debug.Assert(index == 0, "Failure at this line");
        }


        /// <summary>
        /// Example of using Interop.WriteLine within a kernel. Useful for debugging
        /// GPU kernels.
        /// </summary>
        static void Main()
        {
            var values = Enumerable.Range(0, 4).ToArray();

            // Create main context
            using var context = Context.Create(builder =>
            {
                builder.Cuda().OpenCL().AutoAssertions();
                // Alternatives to explore:
                //   builder.Default();
                //   builder.AllAccelerators().Debug();
            });

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(DebugAssertKernel);
                using var buffer = accelerator.Allocate1D(values);

                kernel((int)buffer.Length, buffer.View);

                // Wait for the kernel to finish before the accelerator is disposed
                // at the end of this block.
                accelerator.Synchronize();
            }
        }
    }
}
