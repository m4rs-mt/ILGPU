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
using ILGPU.Algorithms.Random;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;

namespace AlgorithmsRandom
{
    class Program
    {
        /// <summary>
        /// Fills a buffer with random numbers.
        /// </summary>
        static void FillUniform(Accelerator accelerator)
        {
            Console.WriteLine("Fill Buffer");

            // Generate random numbers using the XorShift128Plus algorithm.
            // NB: Use the standard .NET random number generator to initialize each GPU
            // kernel with a different starting seed. Otherwise, if the kernels have the
            // same starting seed, they will generate the same "random number".
            var random = new Random();
            using var rng = RNG.Create<XorShift128Plus>(accelerator, random);

            using var buffer = accelerator.Allocate1D<int>(16);
            rng.FillUniform(accelerator.DefaultStream, buffer.View);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var randomValues = buffer.GetAsArray1D();
            for (int i = 0, e = randomValues.Length; i < e; ++i)
                Console.WriteLine($"Random[{i}] = {randomValues[i]}");

            Console.WriteLine();
        }

        /// <summary>
        /// Generate random numbers within a kernel.
        /// </summary>
        public static void MyRandomKernel(
            Index1D index,
            RNGView<XorShift64Star> rng,
            ArrayView1D<long, Stride1D.Dense> view)
        {
            view[index] = rng.NextLong();
        }

        /// <summary>
        /// Make use of random number generation within a kernel.
        /// </summary>
        static void KernelRandom(Accelerator accelerator)
        {
            Console.WriteLine("Kernel Random");

            // Generate random numbers using the XorShift64Star algorithm.
            // NB: Use the standard .NET random number generator to initialize each GPU
            // kernel with a different starting seed. Otherwise, if the kernels have the
            // same starting seed, they will generate the same "random number".
            var random = new Random();
            using var rng = RNG.Create<XorShift64Star>(accelerator, random);

            // Use the RNG implementation to get a view that is compatible with the given
            // max number of parallel warps. This value is particularly important since
            // this implementation shares a single RNG state across all threads in a warp.
            var rngView = rng.GetView(accelerator.WarpSize);

            using var buffer = accelerator.Allocate1D<long>(16);
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, RNGView<XorShift64Star>, ArrayView1D<long, Stride1D.Dense>>(MyRandomKernel);
            kernel((int)buffer.Length, rngView, buffer.View);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var randomValues = buffer.GetAsArray1D();
            for (int i = 0, e = randomValues.Length; i < e; ++i)
                Console.WriteLine($"Random[{i}] = {randomValues[i]}");

            Console.WriteLine();
        }

        /// <summary>
        /// Generates random numbers using the Nvidia cuRand library.
        /// </summary>
        static void FillCuRand(Accelerator accelerator)
        {
            if (!(accelerator is CudaAccelerator cudaAccelerator))
                return;

            // Use one of the cuRand algorithms to generate random values to populate
            // a GPU buffer.
            Console.WriteLine("CuRand GPU");

            using var rand = CuRand.CreateGPU(cudaAccelerator, CuRandRngType.CURAND_RNG_PSEUDO_DEFAULT);
            rand.SetSeed(1234);

            using var buffer = accelerator.Allocate1D<float>(16);
            rand.FillNormal(accelerator.DefaultStream, buffer.View, mean: 1.5f, stddev: 0.5f);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var randomValues = buffer.GetAsArray1D();
            for (int i = 0, e = randomValues.Length; i < e; ++i)
                Console.WriteLine($"Random[{i}] = {randomValues[i]}");

            Console.WriteLine();

            // cuRand can also be used on the CPU to generate random values.
            Console.WriteLine("CuRand CPU");

            using var cpuRand = CuRand.CreateCPU(accelerator.Context, CuRandRngType.CURAND_RNG_PSEUDO_XORWOW);
            cpuRand.SetSeed(5678);

            var randomCpuValues = new double[16];
            cpuRand.FillUniform(randomCpuValues);
            for (int i = 0, e = randomCpuValues.Length; i < e; ++i)
                Console.WriteLine($"Random[{i}] = {randomCpuValues[i]}");

            Console.WriteLine();
        }

        /// <summary>
        /// Examples of generating random values.
        /// </summary>
        static void Main()
        {
            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            // For each available accelerator...
            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                FillUniform(accelerator);
                KernelRandom(accelerator);
                FillCuRand(accelerator);
            }
        }
    }
}
