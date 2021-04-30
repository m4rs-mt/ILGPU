using ILGPU.Runtime;
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

        public static TheoryData<object> Numbers => new TheoryData<object>
        {
            { 10 },
            { -10 },
            { int.MaxValue },
            { int.MinValue },
        };

        internal static void CopyKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(Numbers))]
        [KernelMethod(nameof(CopyKernel))]
        public void Copy(long constant)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(Length);
            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAcceleratorAsync();
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.GPUView);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAcceleratorAsync();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }

        // No need for kernel, assuming copy tests pass.
        // Just going to confirm integrity in this test.
        [Fact]
        public void GetAsArray()
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                new Index1D(Length));

            for (int i = 0; i < Length; i++)
                exchangeBuffer[i] = 10;
            exchangeBuffer.CopyToAcceleratorAsync();

            var expected = new int[Length];
            for (int i = 0; i < Length; i++)
                expected[i] = 10;

            Accelerator.Synchronize();

            var data = exchangeBuffer.GPUView.GetAs1DArray();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, data.Length);

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], data[i]);
        }

        internal static void CopyAsyncKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data,
            ArrayView1D<long, Stride1D.Dense> data2,
            ArrayView1D<long, Stride1D.Dense> returnBuffer)
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

            exchangeBuffer.CopyToAcceleratorAsync(stream1);
            exchangeBuffer2.CopyToAcceleratorAsync(stream2);
            var expected = Enumerable.Repeat(constant - constant2, Length).ToArray();
            Accelerator.Synchronize();

            Execute(
                exchangeBuffer.IntExtent,
                exchangeBuffer.GPUView,
                exchangeBuffer2.GPUView,
                returnBuffer.GPUView);

            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAcceleratorAsync(stream1);
            exchangeBuffer2.CopyFromAcceleratorAsync(stream2);
            returnBuffer.CopyFromAcceleratorAsync(stream3);
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

        internal static void SpanKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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

            exchangeBuffer.CopyToAcceleratorAsync();
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Length, exchangeBuffer.GPUView);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAcceleratorAsync();
            Accelerator.Synchronize();
            var fromAccelerator = exchangeBuffer.CPUView.GetAs1DArray();

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], fromAccelerator[i]);
        }

        [Theory]
        [InlineData(10, 1024, 0, 1024)]
        [InlineData(10, 1024, 0, 512)]
        [InlineData(10, 1024, 256, 512)]
        [InlineData(10, 1024, 512, 512)]
        [KernelMethod(nameof(CopyKernel))]
        public void CopyToPartially(
            long constant,
            int bufferSize,
            int offset,
            int extent)
        {
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                bufferSize);
            exchangeBuffer.MemSetToZero();

            // Fill data on the CPU side
            for (int i = 0; i < bufferSize; ++i)
                exchangeBuffer[i] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAcceleratorAsync(offset, extent);
            var prefix = Enumerable.Repeat(-5L, offset);
            var infix = Enumerable.Repeat(constant - 5, extent);
            var suffix = Enumerable.Repeat(-5L, bufferSize - extent - offset);
            var expected = prefix.Concat(infix.Concat(suffix)).ToArray();
            Accelerator.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.GPUView);
            Accelerator.Synchronize();

            exchangeBuffer.CopyFromAcceleratorAsync();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < bufferSize; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }

        [Theory]
        [InlineData(10, 1024, 0, 1024)]
        [InlineData(10, 1024, 0, 512)]
        [InlineData(10, 1024, 256, 512)]
        [InlineData(10, 1024, 512, 512)]
        [KernelMethod(nameof(CopyKernel))]
        public void CopyFromPartially(
            long constant,
            int bufferSize,
            int offset,
            int extent)
        {
            using var stream = Accelerator.CreateStream();
            using var exchangeBuffer = Accelerator.AllocateExchangeBuffer<long>(
                bufferSize);
            exchangeBuffer.MemSetToZero(stream);
            stream.Synchronize();

            // Fill data on the CPU side
            for (int i = 0; i < bufferSize; ++i)
                exchangeBuffer[i] = constant;

            // Start copying, create the expected array in the meantime
            exchangeBuffer.CopyToAcceleratorAsync(stream);
            var prefix = Enumerable.Repeat(constant, offset);
            var infix = Enumerable.Repeat(constant - 5, extent);
            var suffix = Enumerable.Repeat(constant, bufferSize - extent - offset);
            var expected = prefix.Concat(infix.Concat(suffix)).ToArray();
            stream.Synchronize();

            Execute(exchangeBuffer.Extent.ToIntIndex(), exchangeBuffer.GPUView);
            stream.Synchronize();

            exchangeBuffer.CopyFromAcceleratorAsync(stream, offset, extent);
            stream.Synchronize();

            Assert.Equal(expected.Length, exchangeBuffer.Length);
            for (int i = 0; i < bufferSize; i++)
                Assert.Equal(expected[i], exchangeBuffer[i]);
        }
    }
}
