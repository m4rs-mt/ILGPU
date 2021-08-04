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
        static void KernelWithGroupExtensions(ArrayView2D<int, Stride2D.DenseX> data)
        {
            var globalIndex = Grid.GlobalIndex.X;

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
            data[globalIndex, 3] = GroupExtensions.AllReduce<int, MinInt32>(Group.IdxX + 1);
        }

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

                var kernel = accelerator.LoadStreamKernel<ArrayView2D<int, Stride2D.DenseX>>(KernelWithGroupExtensions);
                using var buffer = accelerator.Allocate2DDenseX<int>(new Index2D(accelerator.MaxNumThreadsPerGroup, 4));
                kernel((1, buffer.IntExtent.X), buffer.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = buffer.GetAsArray2D();
                for (int i = 0, e = data.GetLength(0); i < e; ++i)
                {
                    for (int j = 0, e2 = data.GetLength(1); j < e2; ++j)
                        Console.WriteLine($"Data[{i}, {j}] = {data[i, j]}");
                }
            }
        }
    }
}
