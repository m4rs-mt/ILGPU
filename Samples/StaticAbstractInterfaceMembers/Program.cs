// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Linq;

namespace StaticAbstractInterfaceMembers
{
    class Program
    {
        // .NET 7 introduced Static Abstract Interface Members. These can either be used
        // by themselves, or combined with Generic Math, to provide the ability to write
        // generic kernels with different implementations for some functionality.
        //
        // Before .NET 7, a more restricted option is to use a struct that implements
        // the generic interface.
        //
#if NET7_0_OR_GREATER
        public interface ICalculatorOperation<T>
            where T : System.Numerics.INumber<T>
        {
            static abstract T Calculate(T left, T right);
        }

        // Interface can be implemented by a 'class' or 'struct'.
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1812:Avoid uninstantiated internal classes",
            Justification = "Using static method in ILGPU kernel")]
        public class AdditionOp : ICalculatorOperation<int>
        {
            public static int Calculate(int left, int right) => left + right;
        }

        // Interface can be implemented by a 'class' or 'struct'.
        public struct MultiplyOp : ICalculatorOperation<float>
        {
            public static float Calculate(float left, float right) => left * right;
        }

        public static void CalculatorKernel<T, TOp>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> input,
            ArrayView1D<T, Stride1D.Dense> output)
            where T : unmanaged, System.Numerics.INumber<T>
            where TOp : ICalculatorOperation<T>
        {
            // Calls the static abstract method on the class or struct.
            output[index] = TOp.Calculate(input[index], input[index]);
        }

#else

        public interface ICalculatorOperation<T>
            where T : unmanaged
        {
            T Calculate(T left, T right);
        }

        // Interface must be implemented by a 'struct'.
        public struct AdditionOp : ICalculatorOperation<int>
        {
            public int Calculate(int left, int right) => left + right;
        }

        // Interface must be implemented by a 'struct'.
        public struct MultiplyOp : ICalculatorOperation<float>
        {
            public float Calculate(float left, float right) => left * right;
        }

        public static void CalculatorKernel<T, TOp>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> input,
            ArrayView1D<T, Stride1D.Dense> output)
            where T : unmanaged
            where TOp : unmanaged, ICalculatorOperation<T>
        {
            // Creates a new instance of the struct, and calls the method.
            output[index] = default(TOp).Calculate(input[index], input[index]);
        }

#endif

        public static void UsingAbstractFunction<T, TOp>(Accelerator accelerator)
#if NET7_0_OR_GREATER
            where T : unmanaged, System.Numerics.INumber<T>
            where TOp : ICalculatorOperation<T>
#else
            where T : unmanaged
            where TOp : unmanaged, ICalculatorOperation<T>
#endif
        {
            var values =
                Enumerable.Range(0, 16)
#if NET7_0_OR_GREATER
                .Select(x => T.CreateChecked(x))
#else
                .Select(x => (T)Convert.ChangeType(
                    x,
                    typeof(T),
                    System.Globalization.CultureInfo.InvariantCulture))
#endif
                .ToArray();
            using var inputBuffer = accelerator.Allocate1D(values);
            using var outputBuffer = accelerator.Allocate1D(values);

            outputBuffer.MemSetToZero();

            var kernel = accelerator.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView1D<T, Stride1D.Dense>,
                ArrayView1D<T, Stride1D.Dense>>(
                CalculatorKernel<T, TOp>);

            kernel(
                (int)inputBuffer.Length,
                inputBuffer.View,
                outputBuffer.View);

            accelerator.Synchronize();

            var result = outputBuffer.GetAsArray1D();
            for (var i = 0; i < result.Length; i++)
                Console.WriteLine($"[{i}] = {result[i]}");
            Console.WriteLine();
        }

        /// <summary>
        /// Demonstrates static abstract members in interfaces.
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

                UsingAbstractFunction<int, AdditionOp>(accelerator);
                UsingAbstractFunction<float, MultiplyOp>(accelerator);
            }
        }
    }
}
