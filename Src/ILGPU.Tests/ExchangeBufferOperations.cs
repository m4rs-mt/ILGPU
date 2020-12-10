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

        internal static void CopyKernel(
            Index1 index,
            ArrayView<long, LongIndex1> data)
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

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator();
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }

        internal static void Copy2DKernel(
            Index2 index,
            ArrayView<long, LongIndex2> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(GetNumbers))]
        [KernelMethod(nameof(Copy2DKernel))]
        public void Copy2D(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index2(Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    exchangeBuffer[new Index2(i, j)] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator();
            var expected = Enumerable.Repeat(constant - 5, Length * Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length * Length; i++)
                Assert.Equal(expected[i], exchangeBuffer.Span[i]);
        }

        internal static void Copy3DKernel(
            Index3 index,
            ArrayView<long, LongIndex3> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(GetNumbers))]
        [KernelMethod(nameof(Copy3DKernel))]
        public void Copy3D(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index3(Length, Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for (int k = 0; k < Length; k++)
                        exchangeBuffer[new Index3(i, j, k)] = constant;

            // Start copying, create the expected array in the meantime.
            exchangeBuffer.CopyToAccelerator();

            var expected = Enumerable.Repeat(constant - 5,
                Length * Length * Length).ToArray();

            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.View);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAccelerator();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length * Length * Length; i++)
                Assert.Equal(expected[i], exchangeBuffer.Span[i]);
        }

        // No need for kernel, assuming copy tests pass.
        // Just going to confirm integrity in this test.
        [Fact]
        public void GetAsArray()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index1(Length));

            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = 10;
            exchangeBuffer.CopyToAccelerator();

            var expected = new int[Length];
            for (int i = 0; i < Length; i++)
                expected[i] = 10;

            Accelerator.Synchronize();

            var data = exchangeBuffer.GetAsArray();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, data.Length);

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], data[i]);
        }

        // No need for kernel, assuming copy tests pass.
        // Just going to confirm integrity in this test.
        [Fact]
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void GetAsArray2D()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
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

            var data = exchangeBuffer.GetAs2DArray();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, data.Length);
            Assert.Equal(expected.GetLength(0), data.GetLength(0));
            Assert.Equal(expected.GetLength(1), data.GetLength(1));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    Assert.Equal(expected[i, j], data[i, j]);
        }

        // No need for kernel, assuming copy tests pass.
        // Just going to confirm integrity in this test.
        [Fact]
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void GetAsArray3D()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
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

            var data = exchangeBuffer.GetAs3DArray();
            Accelerator.Synchronize();

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

        internal static void CopyAsyncKernel(
            Index1 index,
            ArrayView<long, LongIndex1> data,
            ArrayView<long, LongIndex1> data2,
            ArrayView<long, LongIndex1> returnBuffer)
        {
            returnBuffer[index] = data[index] - data2[index];
        }

        // Use the InlineData here, it's going to be more complicated otherwise.
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

            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            using var exchangeBuffer2 = Accelerator.AllocateExchangeBuffer<long>(Length);
            using var returnBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            for (int i = 0; i < Length; i++)
            {
                exchangeBuffer[i] = constant;
                exchangeBuffer2[i] = constant2;
            }

            exchangeBuffer.CopyToAccelerator(stream1);
            exchangeBuffer2.CopyToAccelerator(stream2);
            var expected = Enumerable.Repeat(constant - constant2, Length).ToArray();
            Accelerator.Synchronize();

            Execute(
                exchangeBuffer.Extent.ToIntIndex(),
                exchangeBuffer.View,
                exchangeBuffer2.View,
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

        internal static void SpanKernel(Index1 index, ArrayView<int, LongIndex1> data)
        {
            data[index] = data[index] - 5;
        }

        [Theory]
        [InlineData(10)]
        [KernelMethod(nameof(SpanKernel))]
        public void AsSpan(int constant)
        {
            var exchangeBuffer = Accelerator.AllocateExchangeBuffer<int>(Length);
            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = constant;

            exchangeBuffer.CopyToAccelerator();
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Length, exchangeBuffer.View);
            Accelerator.Synchronize();

            // These should theoretically be the same because GetAsSpan
            // copies into cpuMemory.
            // Syncs on it's own
            Span<int> fromAccelerator = exchangeBuffer.GetAsSpan();

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], fromAccelerator[i]);
        }

        [Fact]
        public void PartiallyCopyExceptionHandling()
        {
            int bufferSize = 1024;
            using var exchangeBuffer =
                    Accelerator.AllocateExchangeBuffer<long>(bufferSize);
            using var stream = Accelerator.CreateStream();

            // CopyTo
            // Check lower bound for accelerator memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, -1));

            // Check upper bound for accelerator memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(bufferSize + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, bufferSize + 1));

            // Check lower bound for cpu memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, -1, 0));

            // Check upper bound for cpu memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(bufferSize + 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, bufferSize + 1, 0));

            // Check lower bound for the extent
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, 0, 0, 0));

            // Check upper bound for the extent
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(0, 0, bufferSize + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(stream, 0, 0, bufferSize + 1));

            // Check if offset + extent > bufferSize
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(bufferSize, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(bufferSize / 2, 0, bufferSize));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(0, bufferSize, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyToAccelerator(0, bufferSize / 2, bufferSize));

            // CopyFrom checks
            // Check lower bound for accelerator memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, -1));

            // Check upper bound for accelerator memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(bufferSize + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, bufferSize + 1));

            // Check lower bound for cpu memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, -1, 0));

            // Check upper bound for cpu memory offset
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(bufferSize + 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, bufferSize + 1, 0));

            // Check lower bound for the extent
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, 0, 0, 0));

            // Check upper bound for the extent
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(0, 0, bufferSize + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(stream, 0, 0, bufferSize + 1));

            // Check if offset + extent > bufferSize
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(bufferSize, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(bufferSize / 2, 0, bufferSize));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(0, bufferSize, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                exchangeBuffer.CopyFromAccelerator(0, bufferSize / 2, bufferSize));
        }

        [Theory]
        [InlineData(10, 1024, 0, 0, 1024)]
        [InlineData(10, 1024, 0, 0, 512)]
        [InlineData(10, 1024, 256, 0, 512)]
        [InlineData(10, 1024, 512, 0, 512)]
        [InlineData(10, 1024, 0, 256, 512)]
        [InlineData(10, 1024, 0, 512, 512)]
        [InlineData(10, 1024, 256, 256, 512)]
        [KernelMethod(nameof(CopyKernel))]
        public void CopyToPartially(
            long constant,
            int bufferSize,
            int cpuOffset,
            int accelOffset,
            int extent)
        {
            using var stream = Accelerator.CreateStream();
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                bufferSize);
            exchangeBuffer.Buffer.MemSetToZero(stream);
            stream.Synchronize();

            // Fill data on the CPU side
            for (int i = 0; i < bufferSize; ++i)
                exchangeBuffer[i] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator(stream, cpuOffset, accelOffset, extent);
            var prefix = Enumerable.Repeat(-5L, accelOffset);
            var infix = Enumerable.Repeat(constant - 5, extent);
            var suffix = Enumerable.Repeat(-5L, bufferSize - extent - accelOffset);
            var expected = prefix.Concat(infix.Concat(suffix)).ToArray();
            stream.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.View);
            stream.Synchronize();

            exchangeBuffer.CopyFromAccelerator(stream);
            stream.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < bufferSize; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }

        [Theory]
        [InlineData(10, 1024, 0, 0, 1024)]
        [InlineData(10, 1024, 0, 0, 512)]
        [InlineData(10, 1024, 256, 0, 512)]
        [InlineData(10, 1024, 512, 0, 512)]
        [InlineData(10, 1024, 0, 256, 512)]
        [InlineData(10, 1024, 0, 512, 512)]
        [InlineData(10, 1024, 256, 256, 512)]
        [KernelMethod(nameof(CopyKernel))]
        public void CopyFromPartially(
            long constant,
            int bufferSize,
            int cpuOffset,
            int accelOffset,
            int extent)
        {
            using var stream = Accelerator.CreateStream();
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                bufferSize);
            exchangeBuffer.Buffer.MemSetToZero(stream);
            stream.Synchronize();

            // Fill data on the CPU side
            for (int i = 0; i < bufferSize; ++i)
                exchangeBuffer[i] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAccelerator(stream);
            var prefix = Enumerable.Repeat(constant, cpuOffset);
            var infix = Enumerable.Repeat(constant - 5, extent);
            var suffix = Enumerable.Repeat(constant, bufferSize - extent - cpuOffset);
            var expected = prefix.Concat(infix.Concat(suffix)).ToArray();
            stream.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.View);
            stream.Synchronize();

            exchangeBuffer.CopyFromAccelerator(stream, accelOffset, cpuOffset, extent);
            stream.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < bufferSize; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }
    }
}
