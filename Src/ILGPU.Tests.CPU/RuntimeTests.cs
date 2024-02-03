// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace ILGPU.Tests.CPU
{
    public class CPURuntimeTests
    {
        internal static void TestCustomDeviceSetup_ImplicitKernel(
            Index1D index,
            ArrayView<int> data)
        {
            data[index] = index;
        }

        internal static void TestCustomDeviceSetup_ExplicitKernel(ArrayView<int> data)
        {
            var globalIndex = Grid.GlobalIndex.X;
            data[globalIndex] = globalIndex;
        }

        [SkippableTheory]
        [InlineData(2, 1, 1)]
        [InlineData(4, 4, 1)]
        [InlineData(2, 1, 64)]
        [InlineData(2, 16, 2)]
        [InlineData(2, 32, 64)]
        [InlineData(32, 8, 1)] // AMD default
        [InlineData(32, 8, 4)]
        [InlineData(32, 8, 16)]
        [InlineData(64, 4, 1)] // Legacy AMD default
        [InlineData(64, 4, 4)]
        [InlineData(16, 8, 1)] // Intel default
        [InlineData(16, 8, 4)]
        [InlineData(16, 8, 8)]
        [InlineData(32, 32, 1)] // Nvidia default
        [InlineData(32, 32, 2)]
        [InlineData(32, 32, 4)]
        public void TestCustomDeviceSetup(
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors)
        {
            // Create a custom CPU device and register it with the context pipeline
            var customDevice = new CPUDevice(
                numThreadsPerWarp,
                numWarpsPerMultiprocessor,
                numMultiprocessors);

            // Skip specific tests on MacOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Detect the number of processors and check whether we run in a supported
                // range of tests
                int maxNumThreads = Environment.ProcessorCount * 256;
                Skip.If(customDevice.NumThreads > maxNumThreads);
            }

            using var context = Context.Create(builder => builder
                .DebugConfig(enableAssertions: true)
                .CPU(customDevice));

            // Spawn a new accelerator and invoke a simple sequence kernel to check
            // whether all threads are actually executed
            using var accl = context.CreateCPUAccelerator(0);

            // Compute the total number of parallel threads
            int numParallelThreads =
                numThreadsPerWarp *
                numWarpsPerMultiprocessor *
                numMultiprocessors;
            Assert.Equal(numParallelThreads, accl.NumThreads);
            Assert.Equal(numParallelThreads, accl.MaxNumThreads);

            // Allocate a buffer for IO purposes
            using var buff = accl.Allocate1D<int>(numParallelThreads);

            // Validate the implicit kernel
            var implicitKernel = accl.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView<int>>(TestCustomDeviceSetup_ImplicitKernel);
            buff.MemSetToZero();
            implicitKernel(buff.IntExtent, buff.View);
            var implicitData = buff.GetAsArray1D();
            for (int i = 0; i < implicitData.Length; ++i)
                Assert.Equal(implicitData[i], i);

            // Validate the explicit kernel
            var explicitKernel = accl.LoadStreamKernel<ArrayView<int>>(
                TestCustomDeviceSetup_ExplicitKernel);
            buff.MemSetToZero();
            explicitKernel(
                (numMultiprocessors, numThreadsPerWarp * numWarpsPerMultiprocessor),
                buff.View);
            var explicitData = buff.GetAsArray1D();
            for (int i = 0; i < explicitData.Length; ++i)
                Assert.Equal(explicitData[i], i);
        }
    }
}
