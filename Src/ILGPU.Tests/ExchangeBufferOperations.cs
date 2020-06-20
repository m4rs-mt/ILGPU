using FluentAssertions.Equivalency;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class ExchangeBufferOperations : TestBase
    {
        private const int Length = 32;

        protected ExchangeBufferOperations(ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        public static IEnumerable<object[]> GetNumbers()
        {
            yield return new object[] { 10 };
            yield return new object[] { -10 };
            yield return new object[] { int.MaxValue };
            yield return new object[] { int.MinValue };
        }


        internal static void CopyKernel(Index1 index, ArrayView<long, Index1> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(GetNumbers))]
        [KernelMethod(nameof(CopyKernel))]
        public void Copy(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = constant;

            //start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator();
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }

        internal static void Copy2DKernel(Index2 index, ArrayView<long, Index2> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(GetNumbers))]
        [KernelMethod(nameof(Copy2DKernel))]
        public void Copy2D(long constant)
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index2(Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    exchangeBuffer[new Index2(i, j)] = constant;

            //start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator();
            var expected = Enumerable.Repeat(constant - 5, Length * Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length * Length; i++)
                Assert.Equal(expected[i], exchangeBuffer.CPUView.BaseView[i]);
        }

        

        internal static void Copy3DKernel(Index3 index, ArrayView<long, Index3> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(GetNumbers))]
        [KernelMethod(nameof(Copy3DKernel))]
        public void Copy3D(long constant)
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index3(Length, Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for (int k = 0; k < Length; k++)
                        exchangeBuffer[new Index3(i, j, k)] = constant;

            //start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator();

            var expected = Enumerable.Repeat(constant - 5,
                Length * Length * Length).ToArray();

            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length * Length * Length; i++)
                Assert.Equal(expected[i], exchangeBuffer.CPUView.BaseView[i]);
        }

        //no need for kernel, assuming copy tests pass.
        //Just going to confirm integrity in this test.
        [Fact]
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void GetAsArray()
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index1(Length));

            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = 10;
            exchangeBuffer.CopyToAccelerator();

            var expected = new int[Length];
            for (int i = 0; i < Length; i++)
                expected[i] = 10;

            Accelerator.Synchronize();

            //synchronizes on it's own
            var data = exchangeBuffer.GetAsArray();

            Assert.Equal(expected.Length, data.Length);;

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], data[i]);
        }

        //no need for kernel, assuming copy tests pass.
        //Just going to confirm integrity in this test.
        [Fact]
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void GetAsArray2D()
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index2(Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    exchangeBuffer[new Index2(i, j)] = 10;
            exchangeBuffer.CopyToAccelerator();

            var expected = new int[Length, Length];
            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    expected[i, j] = 10;

            Accelerator.Synchronize();

            //synchronizes on it's own
            var data = exchangeBuffer.GetAs2DArray();

            Assert.Equal(expected.Length, data.Length);
            Assert.Equal(expected.GetLength(0), data.GetLength(0));
            Assert.Equal(expected.GetLength(1), data.GetLength(1));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    Assert.Equal(expected[i, j], data[i, j]);
        }

        //no need for kernel, assuming copy tests pass.
        //Just going to confirm integrity in this test.
        [Fact]
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void GetAsArray3D()
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index3(Length, Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for (int k = 0; k < Length; k++)
                        exchangeBuffer[new Index3(i, j, k)] = 10;
            exchangeBuffer.CopyToAccelerator();

            var expected = new int[Length, Length, Length];
            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for (int k = 0; k < Length; k++)
                        expected[i, j, k] = 10;

            Accelerator.Synchronize();

            //synchronizes on it's own
            var data = exchangeBuffer.GetAs3DArray();

            Assert.Equal(expected.Length, data.Length);
            Assert.Equal(expected.GetLength(0), data.GetLength(0));
            Assert.Equal(expected.GetLength(1), data.GetLength(1));
            Assert.Equal(expected.GetLength(2), data.GetLength(2));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for (int k = 0; k < Length; k++)
                        Assert.Equal(expected[i, j, k],
                            exchangeBuffer[new Index3(i, j, k)]);
        }

        internal static void CopyAsyncKernel(Index1 index, ArrayView<long, Index1> data,
            ArrayView<long, Index1> data2, ArrayView<long, Index1> returnBuffer)
        {
            returnBuffer[index] = data[index] - data2[index];
        }

        //use the InlineData here, it's going to be more complicated otherwise
        [Theory]
        [InlineData(10, 5)]
        [InlineData(int.MaxValue, 20)]
        [InlineData(int.MinValue, -5)]
        [KernelMethod(nameof(CopyAsyncKernel))]
        public void CopyAsync(long constant, long constant2)
        {
            var stream1 = Accelerator.DefaultStream;
            var stream2 = Accelerator.CreateStream();
            var stream3 = Accelerator.CreateStream();

            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            var exchangeBuffer2 = Accelerator.AllocateExchangeBuffer<long>(Length);
            var returnBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            for (int i = 0; i < Length; i++)
            {
                exchangeBuffer[i] = constant;
                exchangeBuffer2[i] = constant2;
            }

            exchangeBuffer.CopyToAccelerator(stream1);
            exchangeBuffer2.CopyToAccelerator(stream2);
            var expected = Enumerable.Repeat(constant - constant2, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent, exchangeBuffer.View, exchangeBuffer2.View,
                returnBuffer.View);

            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator(stream1);
            exchangeBuffer2.CopyFromAccelerator(stream2);
            returnBuffer.CopyFromAccelerator(stream3);
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            Assert.Equal(expected.Length, exchangeBuffer2.Length);
            Assert.Equal(expected.Length, returnBuffer.Length);
            for (int i = 0; i < Length; i++)
            {
                Assert.Equal(expected[i], returnBuffer[i]);
                Assert.Equal(constant, exchangeBuffer[i]);
                Assert.Equal(constant2, exchangeBuffer2[i]);
            } 
        }
    }
}
