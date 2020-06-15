using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class ExchangeBufferOperations : TestBase
    {

        private const int Length = 32;

        protected ExchangeBufferOperations(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void CopyKernel(Index1 index, ArrayView<int> data)
        {
            data[index] = 5;
        }

        [Fact]
        [KernelMethod(nameof(CopyKernel))]
        public void Copy()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<int>(Length);
            for (int i = 0; i < Length; i++)
            {
                exchangeBuffer[new Index1(i)] = 10;
            }
            exchangeBuffer.CopyToAccelerator();

            var expected = new ArrayView<int>(exchangeBuffer.Buffer, Index1.Zero, exchangeBuffer.Extent);
            for (int i = 0; i < Length; i++)
            {
                expected[new Index1(i)] = 5;
            }

            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            exchangeBuffer.CopyFromAccelerator();
            Assert.Equal(exchangeBuffer.CPUView, expected);
        }

        internal static void Copy2DKernel(Index2 index, ArrayView<int, Index2> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(Copy2DKernel))]
        public void Copy2D(int constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<int>(new Index2(Length, Length));
            for(int i = 0; i < Length; i++)
            {
                for(int j = 0; j < Length; j++)
                {
                    exchangeBuffer[new Index2(i, j)] = constant;
                }
            }
            exchangeBuffer.CopyToAccelerator();

            var expected = new ArrayView<int, Index2>(exchangeBuffer.CPUView.BaseView, exchangeBuffer.Extent);
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    expected[new Index2(i, j)] = constant - 5;
                }
            }

            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            exchangeBuffer.CopyFromAccelerator();
            Assert.Equal(exchangeBuffer.CPUView, expected);
        }

        internal static void GetAsArray2DKernel(Index2 index, ArrayView<int, Index2> data, int c)
        {
            data[index] = c;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(GetAsArray2DKernel))]
        public void GetAsArray2D(int constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<int>(new Index2(Length, Length));
            Execute(exchangeBuffer.Extent, exchangeBuffer.View, constant);

            var expected = new int[Length][];
            for (int i = 0; i < Length; i++)
            {
                expected[i] = new int[Length];
                for (int j = 0; j < Length; j++)
                {
                    expected[i][j] = constant;
                }
            }

            Assert.Equal(expected, exchangeBuffer.GetAs2DArray(Accelerator.DefaultStream));
        }
    }
}
