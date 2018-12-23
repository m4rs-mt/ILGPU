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
using ILGPU.Lightning;
using ILGPU.Lightning.Sequencers;
using ILGPU.ReductionOperations;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.ShuffleOperations;
using System;
using System.Linq;

namespace LightningReduce
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
                    accl.Sequence(buffer.View, new Int32Sequencer());

                    // This overload requires an explicit output buffer but
                    // uses an implicit temporary cache from the associated accelerator.
                    // Call a different overload to use a user-defined memory cache.
                    accl.Reduce(
                        buffer.View,
                        target.View,
                        new ShuffleDownInt32(),
                        new AddInt32());

                    accl.Synchronize();

                    var data = target.GetAsArray();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Reduced[{i}] = {data[i]}");
                }
            }
        }

        /// <summary>
        /// Demonstrates the reduce functionality.
        /// </summary>
        /// <param name="accl">The target accelerator.</param>
        static void AtomicReduce(Accelerator accl)
        {
            using (var buffer = accl.Allocate<int>(64))
            {
                using (var target = accl.Allocate<int>(1))
                {
                    accl.Sequence(buffer.View, new Int32Sequencer());

                    // This overload requires an explicit output buffer but
                    // uses an implicit temporary cache from the associated accelerator.
                    // Call a different overload to use a user-defined memory cache.
                    accl.AtomicReduce(
                        buffer.View,
                        target.View,
                        new ShuffleDownInt32(),
                        new AtomicAddInt32());

                    accl.Synchronize();

                    var data = target.GetAsArray();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"AtomicReduced[{i}] = {data[i]}");
                }
            }
        }

        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator... (without CPU)
                foreach (var acceleratorId in Accelerator.Accelerators.Where(id => id.AcceleratorType != AcceleratorType.CPU))
                {
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        Reduce(accelerator);
                        AtomicReduce(accelerator);
                    }
                }

                // Create custom CPU context with a warp size > 1
                using (var accelerator = new CPUAccelerator(context, 4, 4))
                {
                    Console.WriteLine($"Performing operations on {accelerator}");

                    Reduce(accelerator);
                    AtomicReduce(accelerator);
                }
            }
        }
    }
}
