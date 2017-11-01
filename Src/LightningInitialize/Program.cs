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

namespace LightningInitialize
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

    class Program
    {
        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach(var acceleratorId in Accelerator.Accelerators)
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        using (var buffer = accelerator.Allocate<int>(64))
                        {
                            // Initializes the first half by setting the value to 42.
                            // Note that in this case, the initializer uses the default accelerator stream.
                            accelerator.Initialize(buffer.View.GetSubView(0, buffer.Length / 2), 42);

                            // Initializes the second half by setting the value to 23.
                            // Note that this overload requires an explicit accelerator stream.
                            accelerator.Initialize(accelerator.DefaultStream, buffer.View.GetSubView(buffer.Length / 2), 23);

                            accelerator.Synchronize();

                            var data = buffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Calling the convenient Initialize function on the lightning context
                        // involves internal heap allocations. This can be avoided by constructing
                        // an initializer explicitly:
                        var initializer = accelerator.CreateInitializer<CustomStruct>();

                        using (var buffer2 = accelerator.Allocate<CustomStruct>(64))
                        {
                            // We can now use the initializer without any further heap allocations
                            // during the invocation. Note that the initializer requires an explicit
                            // accelerator stream.
                            initializer(accelerator.DefaultStream, buffer2.View, new CustomStruct()
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
