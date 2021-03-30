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

        internal static void ForCounterDataConstantKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int value = 42;
            int value2 = 23;
            int value3 = 0;
            for (int i = 0; i < 32; ++i)
            {
                ++value;
                --value2;
                value3 += 2;
            }
            data[index] = value + value2 + value3;
        }

        [Fact]
        [KernelMethod(nameof(ForCounterDataConstantKernel))]
        public void ForCounterDataConstant()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(129, Length).ToArray();
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

        internal static void NestedForCounterConstantKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int value = 0;
            for (int i = 0; i < 10; ++i)
            {
                for (int j = 0; j < 20; ++j)
                    value += 2;
            }
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(NestedForCounterConstantKernel))]
        public void NestedForCounterConstant()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(2 * 10 * 20, Length).ToArray();
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

        internal static void DoWhileConstantKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int counter = 38;
            int value = 3;
            do
            {
                ++value;
            }
            while (counter-- > 0);
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(DoWhileConstantKernel))]
        public void DoWhileConstant()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

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

        internal static void NestedBreakContinueConstantKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int accumulate = 0;
            int k = 0;
            for (int i = 0; i < 32; ++i)
            {
                for (int j = 0; j < 13; ++j)
                {
                    if (j == i)
                        continue;

                    if (++k == 9)
                        break;
                }

                if (i == 13)
                    continue;
                ++accumulate;
            }
            data[index] = accumulate;
        }

        [Fact]
        [KernelMethod(nameof(NestedBreakContinueConstantKernel))]
        public void NestedBreakContinueLoopConstant()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(31, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void LoopUnrollingBreakKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int j = 0;
            for (int i = 0; i < 4; ++i)
            {
                ++j;
                if (i == 2) break;
                ++j;
            }
            data[index] = j;
        }

        [Fact]
        [KernelMethod(nameof(LoopUnrollingBreakKernel))]
        public void LoopUnrollingBreak()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(5, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void LoopUnrollingContinueKernel(
            Index1 index,
            ArrayView<int> data)
        {
            int j = 0;
            for (int i = 0; i < 4; ++i)
            {
                ++j;
                if (i == 2) continue;
                ++j;
            }
            data[index] = j;
        }

        [Fact]
        [KernelMethod(nameof(LoopUnrollingContinueKernel))]
        public void LoopUnrollingContinue()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(7, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void LoopUnrollingChainedIfKernel(
            Index1 index,
            ArrayView<int> source,
            ArrayView<int> data)
        {
            for (int i = 0; i < 4; ++i)
            {
                var j = source[i];
                if (i > 2 && i < 5 && i == j)
                    data[index] = j;
            }
        }

        [Fact]
        [KernelMethod(nameof(LoopUnrollingChainedIfKernel))]
        public void LoopUnrollingChainedIf()
        {
            using var source = Accelerator.Allocate<int>(Length);
            using var buffer = Accelerator.Allocate<int>(Length);

            source.MemSetToZero();
            buffer.MemSetToZero();
            Accelerator.Synchronize();

            Execute(buffer.Length, source.View, buffer.View);

            var expected = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void LoopUnrolling_UCE_DCE_Kernel(
            Index1 index,
            ArrayView<int> view)
        {
            int value = 0;

            for (int i = 0; i < 2; i++)
            {
                if (value < 1)
                {
                    int newValue = 0;
                    if (newValue == 0)
                    {
                        value = newValue;
                    }
                }
            }
        }

        [Fact]
        [KernelMethod(nameof(LoopUnrolling_UCE_DCE_Kernel))]
        public void LoopUnrolling_UCE_DCE()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            buffer.MemSetToZero();
            Accelerator.Synchronize();

            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void LoopUnrolling_LICM_Kernel(
            Index1 index,
            ArrayView<int> source,
            ArrayView<int> target)
        {
            int result = LoopUnrolling_LICM_Kernel_1(source);
            result += LoopUnrolling_LICM_Kernel_2(source);
            target[index] = result;
        }

        private static int LoopUnrolling_LICM_Kernel_1(ArrayView<int> a)
        {
            var b = a[0];
            if (b == -1)
                return -1;

            while (true)
            {
                if (b == 13)
                    break;
            }
            return b;
        }

        private static int LoopUnrolling_LICM_Kernel_2(ArrayView<int> a)
        {
            int b = 0;
            for (var i = 0; i < a.Length; i++)
                b = a[i];

            while (true)
            {
                if (b >= 13)
                    break;
            }
            return b;
        }

        [Fact]
        [KernelMethod(nameof(LoopUnrolling_LICM_Kernel))]
        public void LoopUnrolling_LICM()
        {
            using var source = Accelerator.Allocate<int>(Length);
            using var target = Accelerator.Allocate<int>(Length);
            Initialize(source, 13);
            target.MemSetToZero();
            Accelerator.Synchronize();

            Execute(source.Length, source.View, target.View);

            var expected = Enumerable.Repeat(13, Length).ToArray();
            Verify(source, expected);
        }
    }
}

#pragma warning restore CS0162
