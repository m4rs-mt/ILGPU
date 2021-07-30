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
using ILGPU.Runtime;
using System;

namespace AlgorithmsTransform
{
    /// <summary>
    /// A custom structure that can be used in a memory buffer.
    /// </summary>
    public struct CustomStruct
    {
        public int First { get; set; }
        public int Second { get; set; }

        public override string ToString() =>
            $"First: {First}, Second: {Second}";
    }

    /// <summary>
    /// A custom implementation of an int->CustomStruct transformer.
    /// </summary>
    struct IntToCustomStructTransformer : ITransformer<int, CustomStruct>
    {
        /// <summary>
        /// Transforms the given value of type <see cref="int"/>
        /// into a transformed value of type <see cref="CustomStruct"/>.
        /// </summary>
        /// <param name="value">The value to transform.</param>
        /// <returns>The transformed value of type <see cref="CustomStruct"/>.</returns>
        public CustomStruct Transform(int value)
        {
            return new CustomStruct()
            {
                First = value,
                Second = value * value
            };
        }
    }

    class Program
    {
        static void Main()
        {
            // Create default context and enable algorithms library
            using (var context = Context.Create(builder => builder.Default().EnableAlgorithms()))
            {
                // For each available accelerator...
                foreach (var device in context)
                {
                    using (var accelerator = device.CreateAccelerator(context))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        var sourceBuffer = accelerator.Allocate1D<int>(64);
                        accelerator.Initialize(accelerator.DefaultStream, sourceBuffer.View, 2);

                        using (var targetBuffer = accelerator.Allocate1D<CustomStruct>(64))
                        {
                            // Transforms all elements.
                            accelerator.Transform(
                                accelerator.DefaultStream,
                                sourceBuffer.View,
                                targetBuffer.View,
                                new IntToCustomStructTransformer());

                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray1D();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        using (var targetBuffer = accelerator.Allocate1D<CustomStruct>(64))
                        {
                            // Calling the convenient Transform function on the accelerator
                            // involves internal heap allocations. This can be avoided by constructing
                            // a transformer explicitly:
                            var transformer = accelerator.CreateTransformer<int, CustomStruct, IntToCustomStructTransformer>();

                            // We can now use the transformer without any further heap allocations
                            // during the invocation. Note that the transformer requires an explicit
                            // accelerator stream.
                            transformer(
                                accelerator.DefaultStream,
                                sourceBuffer.View,
                                targetBuffer.View,
                                new IntToCustomStructTransformer());

                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray1D();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        sourceBuffer.Dispose();
                    }
                }
            }
        }
    }
}
