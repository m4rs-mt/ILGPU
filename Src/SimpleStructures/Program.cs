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
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace SimpleStructures
{
    class Program
    {
        /// <summary>
        /// A custom structure type.
        /// Sine the <see cref="CPUAccelerator"/> uses dynamic assembly generation
        /// in order to avoid boxing parameters, the dynamically created CPUAccelerator
        /// assembly cannot access this type. Consequently, we have to make this custom
        /// type visible to the <see cref="CPUAccelerator"/>. To this end, we can add
        /// the following assembly attribute to the current asembly:
        /// [assembly: InternalsVisibleTo("CPUAccelerator")] (see AssemblyInfo.cs, 28).
        /// Furthermore, this structure needs to be internally visible to other types in
        /// the current assembly.
        /// </summary>
        internal struct CustomDataType
        {
            private int first;
            private int second;

            public CustomDataType(int value)
            {
                first = value;
                second = value * value;
            }

            public int First => first;
            public int Second => second;
        }

        /// <summary>
        /// A simple 1D kernel that references types which in turn reference internal
        /// custom types of the current assembly.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void MyKernel(
            Index index,
            ArrayView<CustomDataType> dataView)
        {
            dataView[index] = new CustomDataType(index);
        }

        /// <summary>
        /// Demonstrates the correct use of custom internal structure types.
        /// </summary>
        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        using (var loader = new SimpleKernel.SampleKernelLoader())
                        {
                            loader.CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                kernel =>
                                {
                                    using (var buffer = accelerator.Allocate<CustomDataType>(1024))
                                    {
                                        // Launch buffer.Length many threads and pass a view to buffer
                                        kernel.Launch(buffer.Length, buffer.View);

                                        // Wait for the kernel to finish...
                                        accelerator.Synchronize();
                                    }
                                });
                        }
                    }
                }
            }
        }
    }
}
