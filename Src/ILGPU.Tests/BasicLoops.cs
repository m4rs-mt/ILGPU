using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162
#pragma warning disable CA1508 // Avoid dead conditional code

namespace ILGPU.Tests
{
    public abstract class BasicLoops : TestBase
    {
        private const int Length = 128;

        protected BasicLoops(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void WhileFalseKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ForCounterKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, counter);

            var expected = Enumerable.Repeat(42 + counter, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ForCounterDataKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> source)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var source = Accelerator.Allocate1D<int>(Length);
            Initialize(source.View, counter);
            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(42 + counter, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ForCounterDataConstantKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(129, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedForCounterKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, counter1, counter2);

            var expected = Enumerable.Repeat(2 * counter1 * counter2, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedForCounterConstantKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(2 * 10 * 20, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void DoWhileKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, 38);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void DoWhileConstantKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void ContinueKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void BreakKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedBreakContinueKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, counter, counter2, counter3);

            var expected = Enumerable.Repeat(result, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedBreakContinueConstantKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(31, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void LoopUnrollingBreakKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(5, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void LoopUnrollingContinueKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(7, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void LoopUnrollingChainedIfKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<int, Stride1D.Dense> data)
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
            using var source = Accelerator.Allocate1D<int>(Length);
            using var buffer = Accelerator.Allocate1D<int>(Length);

            source.MemSetToZero();
            buffer.MemSetToZero();
            Accelerator.Synchronize();

            Execute(buffer.Length, source.View, buffer.View);

            var expected = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void LoopUnrolling_UCE_DCE_Kernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> view)
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            buffer.MemSetToZero();
            Accelerator.Synchronize();

            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void LoopUnrolling_LICM_Kernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> source,
            ArrayView1D<int, Stride1D.Dense> target)
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
            using var source = Accelerator.Allocate1D<int>(Length);
            using var target = Accelerator.Allocate1D<int>(Length);
            Initialize(source.View, 13);
            target.MemSetToZero();
            Accelerator.Synchronize();

            Execute(source.Length, source.View, target.View);

            var expected = Enumerable.Repeat(13, Length).ToArray();
            Verify(source.View, expected);
        }

        private static void LoopViewSwap_Kernel(
            Index1D index,
            ArrayView1D<(int, int), Stride1D.Dense> target1,
            ArrayView1D<(int, int), Stride1D.Dense> target2,
            ArrayView1D<int, Stride1D.Dense> source1,
            ArrayView1D<int, Stride1D.Dense> source2,
            int numIterations)
        {
            var view1 = source1;
            var view2 = source2;

            for (int i = 0; i < numIterations; ++i)
                Utilities.Swap(ref view1, ref view2);
            target1[index] = (view1[0], view2[0]);

            view1 = source1;
            view2 = source2;
            for (int i = 1; i < numIterations; ++i)
            {
                var t = view1;
                view1 = view2;
                view2 = t;
            }
            target2[index] = (view1[0], view2[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        [InlineData(10)]
        [KernelMethod(nameof(LoopViewSwap_Kernel))]
        public void LoopViewSwap(int numIterations)
        {
            using var target1 = Accelerator.Allocate1D<(int, int)>(Length);
            using var target2 = Accelerator.Allocate1D<(int, int)>(Length);
            var expected = new int[] { int.MinValue, int.MaxValue };
            var expected2 = new int[expected.Length];
            expected.CopyTo(expected2, 0);
            using var source = Accelerator.Allocate1D(expected);
            target1.MemSetToZero();
            target2.MemSetToZero();
            Accelerator.Synchronize();

            Execute(
                Length,
                target1.View,
                target2.View,
                source.View.SubView(0, 1),
                source.View.SubView(1, 1),
                numIterations);

            for (int i = 0; i < numIterations; ++i)
                Utilities.Swap(ref expected[0], ref expected[1]);
            var expectedData = Enumerable.Repeat(
                (expected[0], expected[1]), Length).ToArray();
            Verify(target1.View, expectedData);

            for (int i = 1; i < numIterations; ++i)
                Utilities.Swap(ref expected2[0], ref expected2[1]);
            var expectedData2 = Enumerable.Repeat(
                (expected2[0], expected2[1]), Length).ToArray();
            Verify(target2.View, expectedData2);
        }

        /// <summary>
        /// Wrapper view required by <see cref="DoLoopWithoutEntryBlockKernel(Index1D,
        /// ArrayView{int}, ArrayView{int})"/>.
        /// </summary>
        private readonly struct WrapperView
        {
            public WrapperView(ArrayView<int> source)
            {
                Source = source;
            }

            public ArrayView<int> Source { get; }

            public readonly void GetNonZero(int offset, ref int result)
            {
                do
                {
                    result = Source[offset++];
                }
                while (offset < Source.IntLength & result == 0);
            }
        }

        /// <summary>
        /// Note that this function needs to be compiled with OptimizeCode = true in the
        /// compiler settings. Depending on the SDK being used, this can either occur
        /// in Debug builds with code optimization or release builds.
        /// </summary>
        static void DoLoopWithoutEntryBlockKernel(
            Index1D index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            var view = new WrapperView(source);
            int result = 32;
            view.GetNonZero(index, ref result);
            data[index] = result;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        [InlineData(31)]
        [KernelMethod(nameof(DoLoopWithoutEntryBlockKernel))]
        public void DoLoopWithoutEntryBlock(int length)
        {
            using var target = Accelerator.Allocate1D<int>(length);
            using var source = Accelerator.Allocate1D<int>(length);
            source.MemSetToZero();
            Accelerator.Synchronize();

            Execute(length, target.View.AsContiguous(), source.View.AsContiguous());
            var expected = Enumerable.Repeat(0, length).ToArray();
            Verify(target.View, expected);
        }
    }
}

#pragma warning restore CA1508 // Avoid dead conditional code
#pragma warning restore CS0162
