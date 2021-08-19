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
using ILGPU.Algorithms.HistogramOperations;
using ILGPU.Runtime;
using System;
using System.Linq;

#pragma warning disable CA5394 // Do not use insecure randomness

namespace AlgorithmsHistogram
{
    class Program
    {
        public const int RandomSeed = 12345678;
        public const int NumValues = 16;

        /// <summary>
        /// Places the input value into one of the bins, depending on the number of
        /// bins in the output buffer.
        /// </summary>
        struct CustomModuloBinOperation : IComputeSingleBinOperation<int, Index1D>
        {
            public Index1D ComputeHistogramBin(int value, Index1D numBins)
            {
                return value % numBins.X;
            }
        }

        /// <summary>
        /// A "checked" histogram will indicate to the caller than one of the histogram
        /// bins has overflown - it does not indicate which bin overflowed. The value
        /// of the bin that overflowed will wrap, depending on its data type.
        /// </summary>
        static void SingleBinCheckedHistogram(Accelerator accelerator, int[] values)
        {
            Console.WriteLine("Single bin checked histogram");
            using var buffer = accelerator.Allocate1D(values);

            // Create an histogram with 3 bins.
            using var histogram = accelerator.Allocate1D<long>(3);
            histogram.MemSetToZero();

            // Create a buffer to hold the overflow result.
            using var overflow = accelerator.Allocate1D<int>(1);
            overflow.MemSetToZero();

            accelerator.Histogram<int, Stride1D.Dense, CustomModuloBinOperation>(
                accelerator.DefaultStream,
                buffer.View,
                histogram.View,
                overflow.View);

            var result = histogram.GetAsArray1D();
            for (int i = 0, e = result.Length; i < e; ++i)
                Console.WriteLine($"Histogram[{i}] = {result[i]}");

            var overflowResult = overflow.GetAsArray1D();
            if (overflowResult[0] != 0)
            {
                Console.WriteLine("Histogram overflowed.");
            }
            else
            {
                Console.WriteLine("Histogram did not overflow.");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Increments the X-th bin if that bit is set, for the number of bins in the
        /// output histogram.
        /// </summary>
        struct CustomMultiBinOperation : IComputeMultiBinOperation<int, long, HistogramIncrementInt64>
        {
            public void ComputeHistogramBins(
                int value,
                ArrayView<long> histogram,
                in HistogramIncrementInt64 incrementOperation,
                out bool didOverflow)
            {
                didOverflow = false;

                for (var i = 0; i < histogram.Length; i++)
                {
                    var shiftMask = 1 << i;
                    if ((value & shiftMask) != 0)
                    {
                        // Increment the desired bin, and update the overflow status
                        // after the increment operation.
                        incrementOperation.Increment(
                            ref histogram[i],
                            out var tempDidOverflow);
                        if (tempDidOverflow)
                            didOverflow = true;
                    }
                }
            }
        }

        /// <summary>
        /// Multi-bin histograms allow a single input value to increment multiple bins
        /// of a histogram. They can be created as checked or unchecked.
        /// </summary>
        static void MultiBinUncheckedHistogram(Accelerator accelerator, int[] values)
        {
            Console.WriteLine("Multi bin unchecked histogram");
            using var buffer = accelerator.Allocate1D(values);

            // Create an histogram with 4 bins.
            // The bin operation used in this example will count the number of times
            // the bit in the X-th position is set.
            using var histogram = accelerator.Allocate1D<long>(4);
            histogram.MemSetToZero();

            accelerator.HistogramUnchecked<
                int,
                Stride1D.Dense,
                long,
                HistogramIncrementInt64,
                CustomMultiBinOperation>(
                accelerator.DefaultStream,
                buffer.View,
                histogram.View);

            var result = histogram.GetAsArray1D();
            for (int i = 0, e = result.Length; i < e; ++i)
                Console.WriteLine($"Histogram[{i}] = {result[i]}");

            Console.WriteLine();
        }

        static void Main()
        {
            var rnd = new Random(RandomSeed);
            var values = Enumerable.Range(0, NumValues).Select(x => rnd.Next()).ToArray();

            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            // For each available accelerator...
            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                SingleBinCheckedHistogram(accelerator, values);
                MultiBinUncheckedHistogram(accelerator, values);
            }
        }
    }
}

#pragma warning restore CA5394 // Do not use insecure randomness
