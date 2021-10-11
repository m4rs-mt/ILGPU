﻿// ---------------------------------------------------------------------------------------
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

namespace WarpShuffle
{
    class Program
    {
        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Note that you can use warp-shuffle functionality only within 
        /// explicitly-grouped kernels. Previously, it was required to use one of the predefined
        /// shuffle overloads. If the desired function was not available, you had to create a
        /// custom shuffle operation implementation. The current ILGPU version emits the required
        /// shuffle instructions (even for complex data types) automatically.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ShuffleDownKernel(
            ArrayView<int> dataView)          // A view to a chunk of memory (1D in this case)
        {
            // Use native shuffle-down functionality to shuffle the 
            // given value by a delta of 2 lanes
            int value = Group.IdxX;
            value = Warp.ShuffleDown(value, 2);

            dataView[Grid.GlobalIndex.X] = value;
        }

        /// <summary>
        /// A complex user-defined data structure.
        /// </summary>
        public readonly struct ComplexStruct
        {
            /// <summary>
            /// Constructs a new complex structure.
            /// </summary>
            /// <param name="x">the x value.</param>
            /// <param name="y">The y value.</param>
            /// <param name="z">The z value.</param>
            public ComplexStruct(int x, float y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public int X { get; }
            public float Y { get; }
            public double Z { get; }

            public override string ToString() =>
                $"X: {X}, Y: {Y}, Z: {Z}";
        }

        /// <summary>
        /// A kernel demonstrating the generic ILGPU shuffle functionality: The compiler
        /// automatically generates the required shuffle instructions (even for complex
        /// user-defined data types).
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="value">The value to shuffle.</param>
        static void ShuffleGeneric<T>(
            ArrayView<T> dataView,            // A view to a chunk of memory (1D in this case)
            T value)                          // A constant value
            where T : unmanaged
        {
            // Use intrinsic shuffle functionality to shuffle the 
            // given value from line 0. This does not make much sense in this
            // case since all values will have the same value. However,
            // this demonstrates the generic flexibility of the shuffle instructions.
            value = Warp.Shuffle(value, 0);

            dataView[Grid.GlobalIndex.X] = value;
        }

        /// <summary>
        /// Launches a simple 1D kernel using warp intrinsics.
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

                KernelConfig dimension = (1, accelerator.WarpSize);
                using (var dataTarget = accelerator.Allocate1D<int>(accelerator.WarpSize))
                {
                    // Load the explicitly grouped kernel
                    var shuffleDownKernel = accelerator.LoadStreamKernel<ArrayView<int>>(ShuffleDownKernel);
                    dataTarget.MemSetToZero();

                    shuffleDownKernel(dimension, dataTarget.View);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Shuffle-down kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }

                using (var dataTarget = accelerator.Allocate1D<ComplexStruct>(accelerator.WarpSize))
                {
                    // Load the explicitly grouped kernel
                    var reduceKernel = accelerator.LoadStreamKernel<ArrayView<ComplexStruct>, ComplexStruct>(
                        ShuffleGeneric);
                    dataTarget.MemSetToZero();

                    reduceKernel(dimension, dataTarget.View, new ComplexStruct(2, 40.0f, 16.0));

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Generic shuffle kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }
            }
        }
    }
}
