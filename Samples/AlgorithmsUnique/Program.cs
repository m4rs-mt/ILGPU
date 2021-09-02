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
using ILGPU.Algorithms;
using ILGPU.Algorithms.ComparisonOperations;
using ILGPU.Runtime;
using System;

namespace AlgorithmsUnique
{
    class Program
    {
        /// <summary>
        /// Represents an comparison between two elements of type long.
        /// </summary>
        public readonly struct CustomComparisonInt64
            : IComparisonOperation<long>
        {
            /// <summary>
            /// Compares two elements.
            /// </summary>
            /// <param name="first">The first operand.</param>
            /// <param name="second">The second operand.</param>
            /// <returns>
            /// Less than zero, if first is less than second.
            /// Zero, if first is equal to second.
            /// Greater than zero, if first is greater than second.
            /// </returns>
            public int Compare(long first, long second) =>
                first.CompareTo(second);
        }

        /// <summary>
        /// Copies from the GPU view to the CPU array, but only up to the new length.
        /// </summary>
        static void PrintResults(ArrayView1D<long, Stride1D.Dense> resultView, long newLength)
        {
            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var result = new long[newLength];
            resultView.SubView(0, newLength).CopyToCPU(result);

            for (int i = 0, e = result.Length; i < e; ++i)
                Console.WriteLine($"Result[{i}] = {result[i]}");
        }

        /// <summary>
        /// Use unique extension methods for convenience.
        /// </summary>
        static void ExtensionMethod(Accelerator accelerator, long[] values)
        {
            Console.WriteLine("Extension Method");

            using (var buffer = accelerator.Allocate1D(values))
            {
                var newLength = accelerator.Unique(accelerator.DefaultStream, buffer.View);
                PrintResults(buffer.View, newLength);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Long-form of the kernel using existing comparison operation.
        /// </summary>
        static void UniqueKernel(Accelerator accelerator, long[] values)
        {
            Console.WriteLine("Unique Kernel");

            var uniqueKernel = accelerator.CreateUnique<long, ComparisonInt64>();
            using (var buffer = accelerator.Allocate1D(values))
            {
                // Allocate buffer to hold the new length.
                using var newLengthBuffer = accelerator.Allocate1D<long>(Index1D.One);

                // Allocate buffer for temporary work area.
                var tempSize = accelerator.ComputeUniqueTempStorageSize<long>(buffer.Length);
                using var tempBuffer = accelerator.Allocate1D<int>(tempSize);

                uniqueKernel(
                    accelerator.DefaultStream,
                    buffer.View,
                    newLengthBuffer.View,
                    tempBuffer.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var newLengthArray = newLengthBuffer.GetAsArray1D();
                var newLength = newLengthArray[0];

                PrintResults(buffer.View, newLength);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// // Long-form of the unique kernel using custom comparison operation.
        /// </summary>
        static void CustomComparer(Accelerator accelerator, long[] values)
        {   
            Console.WriteLine("Custom Comparison");

            var customKernel = accelerator.CreateUnique<long, CustomComparisonInt64>();
            using (var buffer = accelerator.Allocate1D(values))
            {
                // Allocate buffer to hold the new length.
                using var newLengthBuffer = accelerator.Allocate1D<long>(Index1D.One);

                // Allocate buffer for temporary work area.
                var tempSize = accelerator.ComputeUniqueTempStorageSize<long>(buffer.Length);
                using var tempBuffer = accelerator.Allocate1D<int>(tempSize);

                customKernel(
                    accelerator.DefaultStream,
                    buffer.View,
                    newLengthBuffer.View,
                    tempBuffer.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var newLengthArray = newLengthBuffer.GetAsArray1D();
                var newLength = newLengthArray[0];

                PrintResults(buffer.View, newLength);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Examples of removing consecutive duplicate values.
        /// </summary>
        static void Main()
        {
            var values = new long[] { 1, 2, 2, 3, 3, 3, 5, 5, 5, 5, 5, 0, 4, 4, 4, 4 };

            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            // For each available accelerator...
            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Remove consecutive duplicate values (in-place).
                // Returns the new length of the view.
                // The remaining contents should be ignored.
                ExtensionMethod(accelerator, values);
                UniqueKernel(accelerator, values);
                CustomComparer(accelerator, values);
            }
        }
    }
}
