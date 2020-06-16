using FluentAssertions.Equivalency;
using ILGPU.Runtime;
using System.Linq;
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

        [Fact]
        public void Copy()
        {
            var stream = Accelerator.CreateStream();
            var buffer = ExchangeBuffer.AllocateExchangeBuffer<int>(Accelerator, Length);

            for (int i = 0; i < Length; i++)
                buffer[i] = 10;
            
            buffer.CopyToAccelerator(stream);
            Accelerator.Synchronize();

            buffer.CopyFromAccelerator(stream);
            Accelerator.Synchronize();

            var expected = Enumerable.Repeat(10, Length).ToArray();

            Assert.Equal(expected.Length, buffer.Extent.X);
            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], buffer[i]);
        }

        [Fact]
        public void Copy2D()
        {
            var stream = Accelerator.CreateStream();
            var buffer = ExchangeBuffer.AllocateExchangeBuffer<int>(Accelerator, new Index2(Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    buffer[new Index2(i, j)] = 10;

            buffer.CopyToAccelerator(stream);
            Accelerator.Synchronize();

            buffer.CopyFromAccelerator(stream);
            Accelerator.Synchronize();

            var expected = Enumerable.Repeat(10, Length * Length).ToArray();

            Assert.Equal(expected.Length, buffer.Length);
            for (int i = 0; i < Length * Length; i++)
                Assert.Equal(buffer.CPUView.BaseView[i], expected[i]);
        }

        [Fact]
        public void Copy3D()
        {
            var stream = Accelerator.CreateStream();
            var buffer = ExchangeBuffer.AllocateExchangeBuffer<int>(Accelerator, new Index3(Length, Length, Length));

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                    for(int k = 0; k < Length; k++)
                        buffer[new Index3(i, j, k)] = 10;

            buffer.CopyToAccelerator(stream);
            Accelerator.Synchronize();

            buffer.CopyFromAccelerator(stream);
            Accelerator.Synchronize();

            var expected = Enumerable.Repeat(10, Length * Length * Length).ToArray();

            Assert.Equal(expected.Length, buffer.Length);
            for (int i = 0; i < Length * Length; i++)
                Assert.Equal(buffer.CPUView.BaseView[i], expected[i]);
        }
    }
}
