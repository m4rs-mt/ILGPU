﻿// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.AtomicOperations;
using ILGPU.Runtime;
using System;

namespace AdvancedAtomics
{
    /// <summary>
    /// Adds two doubles. This implementation can be used in the context of:
    /// Atomic.MakeAtomic, in order to realize custom atomic operations.
    /// </summary>
    struct AddDoubleOperation : IAtomicOperation<double>
    {
        public double Operation(double current, double value) => current + value;
    }

    /// <summary>
    /// Implements an atomic CAS operation for doubles.
    /// Note that this implementation here duplicates functionality from:
    /// ILGPU.AtomicOperations.CompareExchangeDouble.
    /// </summary>
    struct DoubleCompareExchangeOperation : ICompareExchangeOperation<double>
    {
        /// <summary>
        /// Realizes an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        public double CompareExchange(ref double target, double compare, double value)
        {
            return Atomic.CompareExchange(ref target, compare, value);
        }

        /// <summary>
        /// Returns true if both operands represent the same value.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True, if both operands represent the same value.</returns>
        public bool IsSame(double first, double second)
        {
            return first == second;
        }
    }

    /// <summary>
    /// Demonstrates custom atomics using Atomic.MakeAtomic.
    /// CAUTION: This sample might not run on some GPUs due to missing support for atomic functions.
    /// </summary>
    class Program
    {
        /// <summary>
        /// A simple 1D kernel using a custom atomic implementation
        /// of Atomic.Add(ArrayView<double>, double)
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="value">The value to add.</param>
        static void AddDoubleAtomicKernel(
            Index1D index,
            ArrayView<double> dataView,
            double value)
        {
            // atomic add: dataView[0] += value;
            Atomic.MakeAtomic(
                ref dataView[0],
                value,
                new AddDoubleOperation(),
                new DoubleCompareExchangeOperation());
        }

        /// <summary>
        /// A simple 1D kernel using a custom atomic implementation
        /// of Atomic.Add(ArrayView<double>, double) that leverages pre-defined
        /// compare-exchange functionality for doubles.
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="value">The value to add.</param>
        static void AddDoubleAtomicILGPUFunctionsKernel(
            Index1D index,
            ArrayView<double> dataView,
            double value)
        {
            // atomic add: dataView[0] += value;
            Atomic.MakeAtomic(
                ref dataView[0],
                value,
                new AddDoubleOperation(),
                new CompareExchangeDouble());
        }

        /// <summary>
        /// A simple 1D kernel using a pre-defined implementation
        /// of atomic add for doubles.
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="value">The value to add.</param>
        static void AddDoubleBuiltInKernel(
            Index1D index,
            ArrayView<double> dataView,
            double value)
        {
            // atomic add: dataView[0] += value;
            Atomic.Add(ref dataView[0], value);
        }

        static void LaunchKernel(
            Accelerator accelerator,
            Action<Index1D, ArrayView<double>, double> method)
        {
            Console.WriteLine("Launching: " + method.Method.Name);

            var kernel = accelerator.LoadAutoGroupedStreamKernel(method);
            using var buffer = accelerator.Allocate1D<double>(1);
            buffer.MemSetToZero();

            kernel(1024, buffer.View, 2.0);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        /// <summary>
        /// This sample demonstates the use of the Atomic.MakeAtomic
        /// functionality to user defined atomics.
        /// </summary>
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                LaunchKernel(accelerator, AddDoubleAtomicKernel);
                LaunchKernel(accelerator, AddDoubleAtomicILGPUFunctionsKernel);
                LaunchKernel(accelerator, AddDoubleBuiltInKernel);
            }
        }
    }
}
