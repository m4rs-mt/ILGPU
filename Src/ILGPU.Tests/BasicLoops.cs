using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

namespace ILGPU.Tests
{
    public abstract class BasicLoops : TestBase
    {
        private const int Length = 128;

        protected BasicLoops(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void WhileFalseKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int value = 42;
            while (false)
                ++value;
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(WhileFalseKernel))]
        public void WhileFalse()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ForCounterKernel(
            Index1 index,
            ArrayView<int> data,
            int counter)
        {
            int value = 42;
            for (int i = 0; i < counter; ++i)
                ++value;
            data[index] = value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(19)]
        [InlineData(193)]
        [KernelMethod(nameof(ForCounterKernel))]
        public void ForCounter(int counter)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, counter);

            var expected = Enumerable.Repeat(42 + counter, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ForCounterDataKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            int value = 42;
            var counter = source[index];
            for (int i = 0; i < counter; ++i)
                ++value;
            data[index] = value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(19)]
        [InlineData(193)]
        [KernelMethod(nameof(ForCounterDataKernel))]
        public void ForCounterData(int counter)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            using var source = Accelerator.Allocate<int>(Length);
            Initialize(source, counter);
            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(42 + counter, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void NestedForCounterKernel(
            Index1 index,
            ArrayView<int> data,
            int counter,
            int counter2)
        {
            int value = 0;
            for (int i = 0; i < counter; ++i)
            {
                for (int j = 0; j < counter2; ++j)
                    value += 2;
            }
            data[index] = value;
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 10)]
        [InlineData(4, 4)]
        [InlineData(4, 13)]
        [InlineData(19, 13)]
        [KernelMethod(nameof(NestedForCounterKernel))]
        public void NestedForCounter(int counter1, int counter2)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, counter1, counter2);

            var expected = Enumerable.Repeat(2 * counter1 * counter2, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void DoWhileKernel(
            Index1 index,
            ArrayView<int> data,
            int counter)
        {
            int value = 3;
            do
            {
                ++value;
            }
            while (counter-- > 0);
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(DoWhileKernel))]
        public void DoWhile()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, 38);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void ContinueKernel(
            Index1 index,
            ArrayView<int> data,
            int counter,
            int counter2,
            int counter3)
        {
            int accumulate = 1;
            for (int i = 0; i < counter; ++i)
            {
                if (i == counter2)
                    continue;
                ++accumulate;

                if (i == counter3)
                    continue;
                ++accumulate;
            }
            data[index] = accumulate;
        }

        [Theory]
        [InlineData(32, 17, 32, 63)]
        [InlineData(32, 17, 17, 63)]
        [InlineData(32, 32, 17, 64)]
        [InlineData(32, 32, 32, 65)]
        [KernelMethod(nameof(ContinueKernel))]
        public void ContinueLoop(int counter, int counter2, int counter3, int result)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void BreakKernel(
            Index1 index,
            ArrayView<int> data,
            int counter,
            int counter2,
            int counter3)
        {
            int accumulate = 1;
            for (int i = 0; i < counter; ++i)
            {
                if (i == counter2)
                    break;
                ++accumulate;

                if (i == counter3)
                    break;
                ++accumulate;
            }
            data[index] = accumulate;
        }

        [Theory]
        [InlineData(32, 0, 17, 1)]
        [InlineData(32, 17, 3, 8)]
        [InlineData(32, 17, 18, 35)]
        [InlineData(32, 32, 32, 65)]
        [KernelMethod(nameof(BreakKernel))]
        public void BreakLoop(int counter, int counter2, int counter3, int result)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void NestedBreakContinueKernel(
            Index1 index,
            ArrayView<int> data,
            int counter,
            int counter2,
            int counter3)
        {
            int accumulate = 0;
            int k = 0;
            for (int i = 0; i < counter; ++i)
            {
                for (int j = 0; j < counter2; ++j)
                {
                    if (j == i)
                        continue;

                    if (++k == counter3)
                        break;
                }

                if (i == counter2)
                    continue;
                ++accumulate;
            }
            data[index] = accumulate;
        }

        [Theory]
        [InlineData(32, 13, 9, 31)]
        [InlineData(32, 13, int.MaxValue, 31)]
        [InlineData(12, 19, int.MaxValue, 12)]
        [InlineData(12, 19, 7, 12)]
        [KernelMethod(nameof(NestedBreakContinueKernel))]
        public void NestedBreakContinueLoop(
            int counter,
            int counter2,
            int counter3,
            int result)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer, expected);
        }
    }
}

#pragma warning restore CS0162
