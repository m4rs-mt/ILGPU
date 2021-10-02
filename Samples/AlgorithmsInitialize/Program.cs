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
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System;

namespace AlgorithmsInitialize
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

    class Program
    {
        static void Main()
        {
            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            // For each available device...
            foreach (var device in context)
            {
                // Create the associated accelerator
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                using (var buffer = accelerator.Allocate1D<int>(64))
                {
                    // Initializes all values by setting the value to 23.
                    accelerator.Initialize(accelerator.DefaultStream, buffer.View, 23);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    var data = buffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {data[i]}");
                }

                // Calling the convenient Initialize function on the accelerator
                // involves internal heap allocations. This can be avoided by constructing
                // an initializer explicitly:
                var initializer = accelerator.CreateInitializer<CustomStruct, Stride1D.Dense>();

                using (var buffer2 = accelerator.Allocate1D<CustomStruct>(64))
                {
                    // We can now use the initializer without any further heap allocations
                    // during the invocation. Note that the initializer requires an explicit
                    // accelerator stream.
                    initializer(accelerator.DefaultStream, buffer2.View, new CustomStruct()
                    {
                        First = 23,
                        Second = 42
                    });

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    var data = buffer2.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data2[{i}] = {data[i]}");
                }
            }
        }
    }
}
