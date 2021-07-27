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

                        using (var buffer = accelerator.Allocate<int>(64))
                        {
                            // Initializes all values by setting the value to 23.
                            accelerator.Initialize(accelerator.DefaultStream, buffer.View,
                                23);
                            accelerator.Synchronize();

                            var data = buffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Calling the convenient Initialize function on the accelerator
                        // involves internal heap allocations. This can be avoided by constructing
                        // an initializer explicitly:
                        var initializer = accelerator.CreateInitializer<CustomStruct>();

                        using (var buffer2 = accelerator.Allocate<CustomStruct>(64))
                        {
                            // We can now use the initializer without any further heap allocations
                            // during the invocation. Note that the initializer requires an explicit
                            // accelerator stream.
                            initializer(accelerator.DefaultStream, buffer2.View,
                                new CustomStruct()
                                {
                                    First = 23,
                                    Second = 42
                                });

                            accelerator.Synchronize();

                            var data = buffer2.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data2[{i}] = {data[i]}");
                        }
                    }
                }
            }
        }
    }
}
