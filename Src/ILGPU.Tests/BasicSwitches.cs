using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class BasicSwitches : TestBase
    {
        protected BasicSwitches(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void BasicSwitchKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> source)
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
            using var buffer = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var sourceData = new int[length];
            for (int i = 0; i < length; ++i)
            {
                sourceData[i] = i % (length / 2);
            }
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, length);

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

            Verify(buffer, expected);
        }

        internal static void BasicSwitchStoreKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> data2,
            ArrayView<int> source)
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
            using var buffer = Accelerator.Allocate<int>(length);
            using var buffer2 = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var sourceData = new int[length];
            for (int i = 0; i < length; ++i)
            {
                sourceData[i] = i % (length / 2);
            }
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, length);

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

            Verify(buffer, expected);
            Verify(buffer2, expected2);
        }
    }
}
