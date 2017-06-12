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
using ILGPU.Lightning.Sequencers;
using ILGPU.ReductionOperations;
using ILGPU.Runtime;
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
        /// <param name="lc">The target lightning context.</param>
        static void Reduce(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                using (var target = lc.Allocate<int>(1))
                {
                    lc.Sequence(buffer.View, new Int32Sequencer());

                    // This overload requires an explicit output buffer but
                    // uses an implicit temporary cache from the associated accelerator.
                    // Call a different overload to use a user-defined memory cache.
                    lc.Reduce(
                        buffer.View,
                        target.View,
                        new ShuffleDownInt32(),
                        new AddInt32());

                    lc.Synchronize();

                    var data = target.GetAsArray();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Reduced[{i}] = {data[i]}");
                }
            }
        }

        /// <summary>
        /// Demonstrates the reduce functionality.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void AtomicReduce(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                using (var target = lc.Allocate<int>(1))
                {
                    lc.Sequence(buffer.View, new Int32Sequencer());

                    // This overload requires an explicit output buffer but
                    // uses an implicit temporary cache from the associated accelerator.
                    // Call a different overload to use a user-defined memory cache.
                    lc.AtomicReduce(
                        buffer.View,
                        target.View,
                        new ShuffleDownInt32(),
                        new AtomicAddInt32());

                    lc.Synchronize();

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
                foreach (var acceleratorId in LightningContext.Accelerators.Where(id => id.AcceleratorType != AcceleratorType.CPU))
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var lc = LightningContext.CreateContext(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {lc}");

                        Reduce(lc);
                        AtomicReduce(lc);
                    }
                }

                // Create custom CPU context with a warp size > 1
                using (var lc = LightningContext.CreateCPUContext(context, 4, 4))
                {
                    Console.WriteLine($"Performing operations on {lc}");

                    Reduce(lc);
                    AtomicReduce(lc);
                }
            }
        }
    }
}
