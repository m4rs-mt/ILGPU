using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class BasicSwitches : TestBase
    {
        public BasicSwitches(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void BasicSwitchKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            var value = source[index];

            switch (value)
            {
                case 0:
                    value = 7;
                    break;
                case 1:
                    value = 42;
                    break;
                case 2:
                    value = 1337;
                    break;
                default:
                    value = -1;
                    break;
            }

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
                int value;
                switch (sourceData[i])
                {
                    case 0:
                        value = 7;
                        break;
                    case 1:
                        value = 42;
                        break;
                    case 2:
                        value = 1337;
                        break;
                    default:
                        value = -1;
                        break;
                }
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

            switch (value)
            {
                case 0:
                    data[index] = 7;
                    break;
                case 1:
                    data[index] = 42;
                    break;
                case 2:
                    data[index] = 1337;
                    break;
                default:
                    data[index] = -1;
                    break;
            }

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
                int value;
                switch (sourceData[i])
                {
                    case 0:
                        value = 7;
                        break;
                    case 1:
                        value = 42;
                        break;
                    case 2:
                        value = 1337;
                        break;
                    default:
                        value = -1;
                        break;
                }
                expected[i] = value;
                expected2[i] = sourceData[i] + 1;
            }

            Verify(buffer, expected);
            Verify(buffer2, expected2);
        }
    }
}
