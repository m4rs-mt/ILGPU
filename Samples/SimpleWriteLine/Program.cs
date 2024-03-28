// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2021-2024 ILGPU Project
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
using System.Linq;

namespace SimpleWriteLine
{
    class Program
    {
        /// <summary>
        /// Example of using Interop.WriteLine within a kernel to display output.
        /// </summary>
        static void WriteLineKernel(Index1D index, ArrayView<int> dataView)
        {
            // NB: String interpolation, alignment, spacing, format and precision
            // specifiers are not currently supported. Use standard {x} placeholders.
            Interop.WriteLine("{0} = {1}", index, dataView[index]);
        }

        static void Main()
        {
            var values = Enumerable.Range(0, 16).ToArray();

            // Create main context
            using var context = Context.Create(builder =>
            {
                // Need to enable IO operations.
                //
                // Need to enable debugging of optimized kernels. By default,
                // the optimisation is at level 1, which would exclude IO operations.
                builder.Default().DebugConfig(
                    enableIOOperations: true,
                    forceDebuggingOfOptimizedKernels: true);
            });

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(WriteLineKernel);
                using var buffer = accelerator.Allocate1D(values);

                kernel((int)buffer.Length, buffer.View);

                // Interop.WriteLine may require a call to stream.Synchronize() for the
                // contents to be flushed.
                accelerator.DefaultStream.Synchronize();
            }
        }
    }
}
