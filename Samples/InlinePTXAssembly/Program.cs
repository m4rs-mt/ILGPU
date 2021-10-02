// ---------------------------------------------------------------------------------------
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
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;
using System.Globalization;
using System.Numerics;

namespace InlinePTXAssembly
{
    class Program
    {
        /// <summary>
        /// Convenience structure to represent a UInt128 value.
        /// </summary>
        public struct UInt128
        {
            public readonly ulong High;
            public readonly ulong Low;

            public UInt128(ulong high, ulong low)
            {
                High = high;
                Low = low;
            }

            [NotInsideKernel]
            public override string ToString()
            {
                var bi = new BigInteger(High);
                bi <<= 64;
                bi += Low;
                return bi.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Kernel that multiplies two UInt64 values to produce a UInt64 value.
        /// </summary>
        public static void MultiplyUInt128Kernel(
            Index1D index,
            ArrayView<UInt128> buffer,
            SpecializedValue<ulong> constant)
        {
            // NB: Need to convert index.X to ulong, so that %2 will use the correct PTX register type.
            ulong multiplier = (ulong)index.X;

            CudaAsm.Emit("mul.hi.u64 %0, %1, %2;", out ulong high, constant.Value, multiplier);
            CudaAsm.Emit("mul.lo.u64 %0, %1, %2;", out ulong low, constant.Value, multiplier);

            buffer[index] = new UInt128(high, low);
        }

        /// <summary>
        /// Demonstrates using the mul.hi.u64 and mul.lo.u64 inline PTX instructions to
        /// multiply two UInt64 values to produce a UInt128 value.
        /// </summary>
        static void MultiplyUInt128(CudaAccelerator accelerator)
        {
            using var buffer = accelerator.Allocate1D<UInt128>(1024);
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<UInt128>, SpecializedValue<ulong>>(MultiplyUInt128Kernel);
            kernel(
                (int)buffer.Length,
                buffer.View,
                SpecializedValue.New(ulong.MaxValue));

            var results = buffer.GetAsArray1D();
            for (var i = 0; i < results.Length; i++)
                Console.WriteLine($"[{i}] = {results[i]}");
        }

        /// <summary>
        /// Demonstrates a block statement, with local register declaration.
        /// </summary>
        public static void MultipleInstructionKernel(Index1D index, ArrayView<double> view)
        {
            CudaAsm.Emit(
                "{\n\t" +
                "   .reg .f64 t1;\n\t" +       // Declare temp register
                "   add.f64 t1, %1, %2;\n\t" + // Add index with constant into temp register
                "   add.f64 %0, t1, %2;\n\t" + // Add temp register with constant into result
                "}",
                out double result,
                (double)index.X,
                42.0);

            view[index] = result;
        }

        /// <summary>
        /// Demonstrates a block statement, with local register declaration.
        /// </summary>
        static void AddUsingTempRegister(CudaAccelerator accelerator)
        {
            using var buffer = accelerator.Allocate1D<double>(1024);
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>>(MultipleInstructionKernel);
            kernel((int)buffer.Length, buffer.View);

            var results = buffer.GetAsArray1D();
            for (var i = 0; i < results.Length; i++)
                Console.WriteLine($"[{i}] = {results[i]}");
        }

        /// <summary>
        /// Demonstrates some examples of using inline PTX assembly.
        /// </summary>
        static void Main()
        {
            using var context = Context.Create(builder => builder.Cuda());

            foreach (var device in context.GetCudaDevices())
            {
                using var accelerator = device.CreateCudaAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                MultiplyUInt128(accelerator);
                AddUsingTempRegister(accelerator);
            }
        }
    }
}
