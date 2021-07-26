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
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Algorithms.Sequencers;
using ILGPU.Runtime;
using System;

namespace AlgorithmsReduce
{
    class Program
    {
        /// <summary>
        /// Demonstrates the reduce functionality.
        /// </summary>
        /// <param name="accl">The target accelerator.</param>
        static void Reduce(Accelerator accl)
        {
            using (var buffer = accl.Allocate<int>(64))
            {
                using (var target = accl.Allocate<int>(1))
                {
                    accl.Sequence(accl.DefaultStream, buffer.View, new Int32Sequencer());

                    // This overload requires an explicit output buffer but
                    // uses an implicit temporary cache from the associated accelerator.
                    // Call a different overload to use a user-defined memory cache.
                    accl.Reduce<int, AddInt32>(
                        accl.DefaultStream,
                        buffer.View,
                        target.View);

                    accl.Synchronize();

                    var data = target.GetAsArray();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Reduced[{i}] = {data[i]}");
                }
            }
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
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        Reduce(accelerator);
                    }
                }
            }
        }
    }
}
