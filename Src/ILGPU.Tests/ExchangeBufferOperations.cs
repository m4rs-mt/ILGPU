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

        internal static void CopyKernel(Index1 index, ArrayView<long> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(CopyKernel))]
        public void Copy(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            for (int i = 0; i < Length; i++)
            {
                exchangeBuffer[new Index1(i)] = constant;
            }
            exchangeBuffer.CopyToAccelerator();

            var expected = new long[Length];
            for (int i = 0; i < Length; i++)
            {
                expected[i] = constant - 5;
            }

            Accelerator.Synchronize();
            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            var data = exchangeBuffer.CPUView;
            Assert.Equal(data.Length, expected.Length);
            for (int i = 0; i < Length; i++)
            {
                Assert.Equal(expected[i], data[i]);
            }
        }

        internal static void Copy2DKernel(Index2 index, ArrayView<long, Index2> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(Copy2DKernel))]
        public void Copy2D(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(new Index2(Length, Length));
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    exchangeBuffer[new Index2(i, j)] = constant;
                }
            }
            exchangeBuffer.CopyToAccelerator();

            var expected = new long[Length][];
            for (int i = 0; i < Length; i++)
            {
                expected[i] = new long[Length];
                for (int j = 0; j < Length; j++)
                {
                    expected[i][j] = constant - 5;
                }
            }

            Accelerator.Synchronize();
            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            var data = exchangeBuffer.CPUView;
            Assert.Equal(data.Extent.X, expected.Length);
            for (int i = 0; i < Length; i++)
            {
                Assert.Equal(data.Extent.Y, expected[i].Length);
                for (int j = 0; j < Length; j++)
                {
                    Assert.Equal(expected[i][j], data[new Index2(i, j)]);
                }
            }
        }

        internal static void GetAsArray2DKernel(Index2 index, ArrayView<long, Index2> data, long c)
        {
            data[index] = c;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(GetAsArray2DKernel))]
        public void GetAsArray2D(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(new Index2(Length, Length));
            Execute(exchangeBuffer.Extent, exchangeBuffer.View, constant);

            var expected = new long[Length][];
            for (int i = 0; i < Length; i++)
            {
                expected[i] = new long[Length];
                for (int j = 0; j < Length; j++)
                {
                    expected[i][j] = constant;
                }
            }

            var data = exchangeBuffer.GetAs2DArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);
            for (int i = 0; i < Length; i++)
            {
                Assert.Equal(data[i].Length, expected[i].Length);
                for (int j = 0; j < Length; j++)
                {
                    Assert.Equal(data[i][j], expected[i][j]);
                }
            }
        }

        internal static void Copy3DKernel(Index3 index, ArrayView<long, Index3> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [InlineData(10)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [KernelMethod(nameof(Copy3DKernel))]
        public void Copy3D(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(new Index3(Length, Length, Length));
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    for (int k = 0; k < Length; k++)
                    {
                        exchangeBuffer[new Index3(i, j, k)] = constant;
                    }
                }
            }
            exchangeBuffer.CopyToAccelerator();

            var expected = new long[Length][][];
            for (int i = 0; i < Length; i++)
            {
                expected[i] = new long[Length][];
                for (int j = 0; j < Length; j++)
                {
                    expected[i][j] = new long[Length];
                    for (int k = 0; k < Length; k++)
                    {
                        expected[i][j][k] = constant - 5;
                    }
                }
            }

            Accelerator.Synchronize();
            Execute(exchangeBuffer.Extent, exchangeBuffer.View);
            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            var data = exchangeBuffer.CPUView;
            Assert.Equal(data.Extent.X, expected.Length);
            for (int i = 0; i < Length; i++)
            {
                Assert.Equal(data.Extent.Y, expected[i].Length);
                for (int j = 0; j < Length; j++)
                {
                    Assert.Equal(data.Extent.Z, expected[i][j].Length);
                    for (int k = 0; k < Length; k++)
                    {
                        Assert.Equal(expected[i][j][k], data[new Index3(i, j, k)]);
                    }
                }
            }
        }
    }
}
