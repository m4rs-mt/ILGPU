// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Linq;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class GenericMath : TestBase
    {
        protected GenericMath(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

#if NET7_0_OR_GREATER

        private const int Length = 1024;

        public static T GeZeroIfBigger<T>(T value, T max) where T : INumber<T>
        {
            if (value > max)
                return T.Zero;
            return value;
        }

        internal static void GenericMathKernel<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> input,
            ArrayView1D<T, Stride1D.Dense> output,
            T maxValue)
            where T : unmanaged, INumber<T>
        {
            output[index] = GeZeroIfBigger(input[index], maxValue);
        }

        private void TestGenericMathKernel<T>(T[] inputValues, T[] expected, T maxValue)
            where T : unmanaged, INumber<T>
        {
            using var input = Accelerator.Allocate1D<T>(inputValues);
            using var output = Accelerator.Allocate1D<T>(Length);

            using var start = Accelerator.DefaultStream.AddProfilingMarker();
            Accelerator.LaunchAutoGrouped<
                Index1D,
                ArrayView1D<T, Stride1D.Dense>,
                ArrayView1D<T, Stride1D.Dense>,
                T>(
                GenericMathKernel,
                Accelerator.DefaultStream,
                (int)input.Length,
                input.View,
                output.View,
                maxValue);

            Verify(output.View, expected);
        }

        [Fact]
        public void GenericMathIntTest()
        {
            const int MaxValue = 50;
            var input = Enumerable.Range(0, Length).ToArray();

            var expected = input
                .Select(x => GeZeroIfBigger(x, MaxValue))
                .ToArray();

            TestGenericMathKernel(input, expected, MaxValue);
        }

        [Fact]
        public void GenericMathDoubleTest()
        {
            const double MaxValue = 75.0;
            var input = Enumerable.Range(0, Length)
                .Select(x => (double)x)
                .ToArray();

            var expected = input
                .Select(x => GeZeroIfBigger(x, MaxValue))
                .ToArray();

            TestGenericMathKernel(input, expected, MaxValue);
        }

#endif
    }
}
