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
using ILGPU.Runtime;
using System;

namespace AlgorithmsGroups
{
    class Program
    {
        /// <summary>
        /// An explicitly grouped kernel that uses high-level group extensions.
        /// Use the available scan/reduce operations in the namespace ILGPU.Algorithms.ScanReduceOperations.
        /// </summary>
        static void KernelWithGroupExtensions(GroupedIndex index, ArrayView2D<int> data)
        {
            var globalIndex = index.ComputeGlobalIndex();

            // Use the all-reduce algorithm to perform a reduction over all lanes in a warp.
            // Every lane in the warp will receive the resulting value.
            // Use WarpExtensions.Reduce for faster performance, if you need to have the result
            // in the first lane only.
            data[globalIndex, 0] = GroupExtensions.AllReduce<int, AddInt32>(1);

            // Perform an exclusive scan over all lanes in the whole warp.
            data[globalIndex, 1] = GroupExtensions.ExclusiveScan<int, AddInt32>(1);

            // Perform an inclusive scan over all lanes in the whole warp.
            data[globalIndex, 2] = GroupExtensions.InclusiveScan<int, AddInt32>(1);

            // Perform a all reduction using a different reduction logic.
            data[globalIndex, 3] = GroupExtensions.AllReduce<int, MinInt32>(index.GroupIdx + 1);
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

                        var kernel = accelerator.LoadStreamKernel<GroupedIndex, ArrayView2D<int>>(KernelWithGroupExtensions);
                        using (var buffer = accelerator.Allocate<int>(accelerator.MaxNumThreadsPerGroup, 4))
                        {
                            kernel(new GroupedIndex(1, buffer.Width), buffer.View);
                            accelerator.Synchronize();

                            var data = buffer.GetAs2DArray();
                            for (int i = 0, e = data.GetLength(0); i < e; ++i)
                            {
                                for (int j = 0, e2 = data.GetLength(1); j < e2; ++j)
                                    Console.WriteLine($"Data[{i}, {j}] = {data[i, j]}");
                            }
                        }
                    }
                }
            }
        }
    }
}
