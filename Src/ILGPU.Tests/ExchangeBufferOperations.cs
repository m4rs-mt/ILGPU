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
            data[new Index2(index.X, index.Y)] = 5;
        }

        [Fact]
        [KernelMethod(nameof(Copy2DKernel))]
        public void Copy2D()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<int>(new Index2(Length, Length));
            for(int i = 0; i < Length; i++)
            {
                for(int j = 0; j < Length; j++)
                {
                    exchangeBuffer[new Index2(i, j)] = 10;
                }
            }
            exchangeBuffer.CopyToAccelerator();
            Execute(exchangeBuffer.Extent, exchangeBuffer.View);

            var expected = new ArrayView<int, Index2>(exchangeBuffer.CPUView.BaseView, exchangeBuffer.Extent);
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    expected[new Index2(i, j)] = 5;
                }
            }

            exchangeBuffer.CopyFromAccelerator();
            Assert.Equal(exchangeBuffer.CPUView, expected);
        }
    }
}
