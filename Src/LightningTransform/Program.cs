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
using ILGPU.Lightning;
using ILGPU.Runtime;
using System;

namespace LightningTransform
{
    /// <summary>
    /// A custom structure that can be used in a memory buffer.
    /// </summary>
    struct CustomStruct
    {
        public int First;
        public int Second;

        public override string ToString()
        {
            return $"First: {First}, Second: {Second}";
        }
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
        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        var sourceBuffer = accelerator.Allocate<int>(64);
                        accelerator.Initialize(sourceBuffer.View, 2);

                        using (var targetBuffer = accelerator.Allocate<CustomStruct>(64))
                        {
                            // Transforms the first half.
                            // Note that the transformer uses the default accelerator stream in this case.
                            accelerator.Transform(
                                sourceBuffer.View.GetSubView(0, sourceBuffer.Length / 2),
                                targetBuffer.View,
                                new IntToCustomStructTransformer());

                            // Transforms the second half.
                            // Note that this overload requires an explicit accelerator stream.
                            accelerator.Transform(
                                accelerator.DefaultStream,
                                sourceBuffer.View.GetSubView(sourceBuffer.Length / 2),
                                targetBuffer.View.GetSubView(sourceBuffer.Length / 2),
                                new IntToCustomStructTransformer());

                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        using (var targetBuffer = accelerator.Allocate<CustomStruct>(64))
                        {
                            // Calling the convenient Transform function on the lightning context
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

                            var data = targetBuffer.GetAsArray();
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
