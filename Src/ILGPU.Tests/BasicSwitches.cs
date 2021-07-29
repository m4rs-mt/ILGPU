using ILGPU.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class BasicSwitches : TestBase
    {
        protected BasicSwitches(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void BasicSwitchKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            var value = source[index] switch
            {
                0 => 7,
                1 => 42,
                2 => 1337,
                _ => -1,
            };
            data[index] = value;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(57)]
        [InlineData(1024)]
        [KernelMethod(nameof(BasicSwitchKernel))]
        public void BasicSwitch(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            var sourceData = new int[length];
            for (int i = 0; i < length; ++i)
            {
                sourceData[i] = i % (length / 2);
            }

            source.CopyFromCPU(Accelerator.DefaultStream, sourceData);

            Execute(length, buffer.View, source.View);

            var expected = new int[length];
            for (int i = 0; i < length; ++i)
            {
                var value = sourceData[i] switch
                {
                    0 => 7,
                    1 => 42,
                    2 => 1337,
                    _ => -1,
                };
                expected[i] = value;
            }

            Verify(buffer.View, expected);
        }

        internal static void BasicSwitchStoreKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            ArrayView1D<int, Stride1D.Dense> source)
        {
            var value = source[index];

            data[index] = value switch
            {
                0 => 7,
                1 => 42,
                2 => 1337,
                _ => -1,
            };
            data2[index] = value + 1;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(57)]
        [InlineData(1024)]
        [KernelMethod(nameof(BasicSwitchStoreKernel))]
        public void BasicSwitchStore(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            using var buffer2 = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            var sourceData = new int[length];
            for (int i = 0; i < length; ++i)
            {
                sourceData[i] = i % (length / 2);
            }

            source.CopyFromCPU(Accelerator.DefaultStream, sourceData);

            Execute(length, buffer.View, buffer2.View, source.View);

            var expected = new int[length];
            var expected2 = new int[length];
            for (int i = 0; i < length; ++i)
            {
                var value = sourceData[i] switch
                {
                    0 => 7,
                    1 => 42,
                    2 => 1337,
                    _ => -1,
                };
                expected[i] = value;
                expected2[i] = sourceData[i] + 1;
            }

            Verify(buffer.View, expected);
            Verify(buffer2.View, expected2);
        }
    }
}
