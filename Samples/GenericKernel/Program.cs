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

namespace GenericKernel
{
    /// <summary>
    /// An interface constraint for the <see cref="Kernel{TKernelFunction, T}(Index1D, ArrayView{T}, int, TKernelFunction)"/> function.
    /// This helps to emulate a lambda-function delegate that is passed to a kernel in a type safe way.
    /// </summary>
    /// <typeparam name="T">The element type that is returned by the <see cref="ComputeValue(Index1D, int)"/> function.</typeparam>
    interface IKernelFunction<T>
        where T : struct
    {
        /// <summary>
        /// Computes a domain-specific value.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <param name="value">The kernel-context specific value.</param>
        /// <returns>The computed value.</returns>
        T ComputeValue(Index1D index, int value);
    }

    /// <summary>
    /// Implements a custom lambda closure
    /// </summary>
    readonly struct LambdaClosure : IKernelFunction<long>
    {
        /// <summary>
        /// Constructs a new lambda closure.
        /// </summary>
        /// <param name="offset">The offset to use.</param>
        public LambdaClosure(long offset)
        {
            Offset = offset;
        }

        /// <summary>
        /// Returns the offset to add to all elements.
        /// </summary>
        public long Offset { get; }

        /// <summary cref="IKernelFunction{T}.ComputeValue(Index1D, int)"/>
        public long ComputeValue(Index1D index, int value) =>
            Offset + value * index;
    }

    class Program
    {
        /// <summary>
        /// A generic kernel that uses generic arguments to emulate a lambda-function delegate.
        /// </summary>
        /// <typeparam name="TKernelFunction">The custom kernel functionality.</typeparam>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="index">The element index.</param>
        /// <param name="data">The target data array.</param>
        /// <param name="value">The constant input value.</param>
        /// <param name="function">The domain and context-specific kernel lambda function.</param>
        static void Kernel<TKernelFunction, T>(
            Index1D index,
            ArrayView<T> data,
            int value,
            TKernelFunction function)
            where TKernelFunction : struct, IKernelFunction<T>
            where T : unmanaged
        {
            // Invoke the custom "lambda function"
            data[index] = function.ComputeValue(index, value);
        }

        /// <summary>
        /// Demonstrates generic kernel functions to simulate lambda closures via generic types.
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
                    using (var accelerator = device.CreateAccelerator(context))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        var kernel = accelerator.LoadAutoGroupedStreamKernel<
                            Index1D, ArrayView<long>, int, LambdaClosure>(Kernel);
                        using (var buffer = accelerator.Allocate1D<long>(DataSize))
                        {
                            kernel((int)buffer.Length, buffer.View, 1, new LambdaClosure(20));

                            var data = buffer.GetAsArray1D();
                        }
                    }
                }
            }
        }
    }
}
