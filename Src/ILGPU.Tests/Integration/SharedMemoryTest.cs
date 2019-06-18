using System;
using System.Linq;
using ILGPU.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests.Integration
{
    public class SharedMemoryTest
    {
        public ITestOutputHelper OutputHelper { get; }

        public SharedMemoryTest(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        [Theory]
        [MemberData(nameof(TestConfigurations.Default), MemberType = typeof(TestConfigurations))]
        public void SharedMemoryVariableKernelTest(TestConfiguration config)
        {
            using (var context = config.CreateContext())
            using (var accelerator = config.CreateAccelerator(context))
            {
                OutputHelper.WriteLine($"Performing operations on {accelerator}");

                // The maximum group size in this example is 128 since the second
                // kernel has a shared-memory array of 128 elements.
                var groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 128);

                var data = Enumerable.Range(1, 128).ToArray();

                using (var dataSource = accelerator.Allocate<int>(data.Length))
                using (var dataTarget = accelerator.Allocate<int>(data.Length))
                {
                    // Initialize data source
                    dataSource.CopyFrom(data, 0, 0, data.Length);

                    var dimension = new GroupedIndex(
                        (dataSource.Length + groupSize - 1) / groupSize, // Compute the number of groups (round up)
                        groupSize);                                    // Use the given group size

                    var sharedMemVarKernel = accelerator.LoadStreamKernel<
                        GroupedIndex, ArrayView<int>, ArrayView<int>>(SharedMemoryVariableKernel);
                    dataTarget.MemSetToZero();

                    // Note that shared memory cannot be accessed from the outside
                    // and must be initialized by the kernel
                    sharedMemVarKernel(dimension, dataSource.View, dataTarget.View);

                    accelerator.Synchronize();

                    OutputHelper.WriteLine("Shared-memory kernel");
                    var target = dataTarget.GetAsArray();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        OutputHelper.WriteLine($"Data[{i}] = {target[i]}");
                }
            }
        }

        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Shared memory is only supported in explicitly-grouped kernel contexts and can be accesses
        /// via the static <see cref="ILGPU.SharedMemory"/> class.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void SharedMemoryVariableKernel(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<int> dataView,          // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView)        // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // 'Allocate' a single shared memory variable of type int (= 4 bytes)
            ref int sharedVariable = ref ILGPU.SharedMemory.Allocate<int>();

            // Initialize shared memory
            if (index.GroupIdx.IsFirst)
                sharedVariable = 0;
            // Wait for the initialization to complete
            Group.Barrier();

            if (globalIndex < dataView.Length)
                Atomic.Max(ref sharedVariable, dataView[globalIndex]);

            // Wait for all threads to complete the maximum computation process
            Group.Barrier();

            // Write the maximum of all values into the data view
            if (globalIndex < outputView.Length)
                outputView[globalIndex] = sharedVariable;
        }
    }
}
