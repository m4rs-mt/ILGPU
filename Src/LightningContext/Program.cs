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
using System;

namespace LightningContextSample
{
    class Program
    {
        /// <summary>
        /// Prints accelerator information of the given lightning context.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void PrintLightningContextInfo(LightningContext lc)
        {
            Console.WriteLine($"Name: {lc.Name}");
            Console.WriteLine($"MemorySize: {lc.MemorySize}");
            Console.WriteLine($"MaxThreadsPerGroup: {lc.MaxThreadsPerGroup}");
            Console.WriteLine($"MaxSharedMemoryPerGroup: {lc.MaxSharedMemoryPerGroup}");
            Console.WriteLine($"MaxGridSize: {lc.MaxGridSize}");
            Console.WriteLine($"MaxConstantMemory: {lc.MaxConstantMemory}");
            Console.WriteLine($"WarpSize: {lc.WarpSize}");
            Console.WriteLine($"NumMultiprocessors: {lc.NumMultiprocessors}");
        }

        /// <summary>
        /// Initializes an ILGPU lightning context.
        /// </summary>
        static void Main(string[] args)
        {
            // Every application needs an instantiated global ILGPU context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach(var acceleratorId in LightningContext.Accelerators)
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var lc = LightningContext.CreateContext(context, acceleratorId))
                    {
                        // A lightning context already provides all required functionality
                        // to compile, load and cache kernels. Furthermore, it offers
                        // additional utility functional that is demostrated in other samples.

                        // You can access the kernel cache via:
                        // lc.CompiledKernelCache
                        // You can access the automatically created backend and compile unit via:
                        // lc.Backend and lc.CompileUnit

                        // By default, the lightning context hosts a memory-buffer cache
                        // for operations that require a temporary cache. You can also
                        // access the cache via:
                        // lc.DefaultCache

                        PrintLightningContextInfo(lc);
                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
