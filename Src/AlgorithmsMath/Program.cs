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

namespace AlgorithmsMath
{
    class Program
    {
        /// <summary>
        /// A custom kernel using <see cref="XMath"/> functions.
        /// </summary>
        public static void KernelWithXMath(Index index, ArrayView<float> data, float c)
        {
            data[index] = XMath.Sinh(c + index) + XMath.Atan(c);
        }

        /// <summary>
        /// A custom kernel leveraging <see cref="Math"/> functions that will be internally
        /// remapped to their <see cref="XMath"/> counterparts.
        /// </summary>
        /// <remarks>
        /// .Net Core supports the MathF class that has 32bit-float implementations for most
        /// math functions. These functions will also be automatically remapped to their
        /// corresponding counterparts (if possible).
        /// </remarks>
        public static void KernelWithMath(Index index, ArrayView<float> data, float c)
        {
            data[index] = (float)(Math.Sinh(c + index) + Math.Atan(c));
        }

        static void Main()
        {
            using (var context = new Context())
            {
                // Enable algorithms library
                context.EnableAlgorithms();

                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create the associated accelerator
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        using (var buffer = accelerator.Allocate<float>(64))
                        {
                            void WriteData()
                            {
                                accelerator.Synchronize();
                                var data = buffer.GetAsArray();
                                for (int i = 0, e = data.Length; i < e; ++i)
                                    Console.WriteLine($"Data[{i}] = {data[i]}");
                            }

                            Console.WriteLine(nameof(KernelWithXMath));
                            var xmathKernel = accelerator.LoadAutoGroupedStreamKernel<Index, ArrayView<float>, float>(
                                KernelWithXMath);
                            xmathKernel(buffer.Length, buffer, 0.1f);
                            WriteData();

                            Console.WriteLine(nameof(KernelWithMath));
                            var mathKernel = accelerator.LoadAutoGroupedStreamKernel<Index, ArrayView<float>, float>(
                                KernelWithMath);
                            mathKernel(buffer.Length, buffer, 0.1f);
                            WriteData();
                        }
                    }
                }
            }
        }
    }
}
