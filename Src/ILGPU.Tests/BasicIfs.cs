using ILGPU.Runtime;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

namespace ILGPU.Tests
{
    public abstract class BasicIfs : TestBase
    {
        private const int Length = 32;

        protected BasicIfs(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void IfTrueKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value;
            if (true)
                value = 42;
            else
                value = 23;
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(IfTrueKernel))]
        public void IfTrue()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void IfFalseKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value;
            if (false)
                value = 42;
            else
                value = 23;
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(IfFalseKernel))]
        public void IfFalse()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void IfTrueSideEffectsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2)
        {
            if (true)
            {
                data[index] = 42;
                data2[index] = 1;
            }
            else
            {
                data[index] = 23;
                data2[index] = 0;
            }
        }

        [Fact]
        [KernelMethod(nameof(IfTrueSideEffectsKernel))]
        public void IfTrueSideEffects()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var buffer2 = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);

            var expected2 = Enumerable.Repeat(1, Length).ToArray();
            Verify(buffer2.View, expected2);
        }

        internal static void IfFalseSideEffectsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2)
        {
            if (false)
            {
                data[index] = 42;
                data2[index] = 1;
            }
            else
            {
                data[index] = 23;
                data2[index] = 0;
            }
        }

        [Fact]
        [KernelMethod(nameof(IfFalseSideEffectsKernel))]
        public void IfFalseSideEffects()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var buffer2 = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer.View, expected);

            var expected2 = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer2.View, expected2);
        }

        internal static void IfSideEffectsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            int c)
        {
            if (c != 0)
            {
                data[index] = 42;
                data2[index] = 1;
            }
            else
            {
                data[index] = 23;
                data2[index] = 0;
            }
        }

        [Theory]
        [InlineData(1, 42, 1)]
        [InlineData(int.MaxValue, 42, 1)]
        [InlineData(int.MinValue, 42, 1)]
        [InlineData(0, 23, 0)]
        [KernelMethod(nameof(IfSideEffectsKernel))]
        public void IfSideEffects(int c, int res1, int res2)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var buffer2 = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View, c);

            var expected = Enumerable.Repeat(res1, Length).ToArray();
            Verify(buffer.View, expected);

            var expected2 = Enumerable.Repeat(res2, Length).ToArray();
            Verify(buffer2.View, expected2);
        }

        internal static void IfNestedSideEffectsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            int c,
            int d)
        {
            if (c != 0)
            {
                if (d == 0)
                {
                    data[index] = 42;
                    data2[index] = 1;
                }
                else
                {
                    data[index] = 43;
                    data2[index] = 2;
                }
            }
            else
            {
                if (d != 0)
                {
                    data[index] = 23;
                    data2[index] = 3;
                }
                else
                {
                    data[index] = 24;
                    data2[index] = 4;
                }
            }
        }

        [Theory]
        [InlineData(1, 0, 42, 1)]
        [InlineData(int.MaxValue, 0, 42, 1)]
        [InlineData(int.MinValue, 0, 42, 1)]
        [InlineData(1, 1, 43, 2)]
        [InlineData(int.MaxValue, int.MaxValue, 43, 2)]
        [InlineData(int.MinValue, int.MinValue, 43, 2)]
        [InlineData(0, 0, 24, 4)]
        [InlineData(0, 1, 23, 3)]
        [InlineData(0, int.MaxValue, 23, 3)]
        [InlineData(0, int.MinValue, 23, 3)]
        [KernelMethod(nameof(IfNestedSideEffectsKernel))]
        public void IfNestedSideEffects(int c, int d, int res1, int res2)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var buffer2 = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View, c, d);

            var expected = Enumerable.Repeat(res1, Length).ToArray();
            Verify(buffer.View, expected);

            var expected2 = Enumerable.Repeat(res2, Length).ToArray();
            Verify(buffer2.View, expected2);
        }

        internal static void IfAndOrKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value;

            if ((index.X == 0 || index.X == 1) && index.X <= 2)
                value = 42;
            else
                value = 0;

            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(IfAndOrKernel))]
        public void IfAndOr()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, 2).Concat
                (Enumerable.Repeat(0, Length - 2)).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedIfAndOrKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            int c,
            int d)
        {
            int value = d;
            if (c < 23)
            {
                if ((index.X == 0 || index.X == 1) && index.X <= 2)
                    value = 42;
                if ((index.X == 3 || index.X == 4 || index.X <= 5) && c < 42)
                    value = 43;
            }
            else if (c == 23 || c < 43 && c > d)
            {
                value = 24;
            }

            data[index] = value;
        }

        [Theory]
        [InlineData(0, 1, 43, 1)]
        [InlineData(43, 1, 1, 1)]
        [InlineData(23, 1, 24, 24)]
        [InlineData(24, 1, 24, 24)]
        [KernelMethod(nameof(NestedIfAndOrKernel))]
        public void NestedIfAndOr(int c, int d, int res1, int res2)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, c, d);

            var expected = Enumerable.Repeat(res1, 6).Concat(
                Enumerable.Repeat(res2, Length - 6)).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void NestedIfAndOrKernelSideEffects(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            int c,
            int d)
        {
            if (c < 23)
            {
                if ((index.X == 0 || index.X == 1) && index.X <= 2)
                    data[index] = 42;
                if ((index.X == 3 || index.X == 4 || index.X <= 5) && c < 42)
                {
                    data[index] = 43;
                    return;
                }
            }
            else if (c == 23 || c < 43 && c > d)
            {
                data[index] = 24;
                return;
            }
            data[index] = d;
        }

        [Theory]
        [InlineData(0, 1, 43, 1)]
        [InlineData(43, 1, 1, 1)]
        [InlineData(23, 1, 24, 24)]
        [InlineData(24, 1, 24, 24)]
        [KernelMethod(nameof(NestedIfAndOrKernelSideEffects))]
        public void NestedIfAndOrSideEffects(int c, int d, int res1, int res2)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, c, d);

            var expected = Enumerable.Repeat(res1, 6).Concat(
                Enumerable.Repeat(res2, Length - 6)).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void IfWithoutBlocksKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            int a,
            int b,
            int c)
        {
            data[index] = a > 0 ? b : c;
            data2[index] = a <= 0 ? b : c;
        }

        [Fact]
        [KernelMethod(nameof(IfWithoutBlocksKernel))]
        public void IfWithoutBlocks()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            using var buffer2 = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View, 23, 42, 23);

            Verify(buffer.View, Enumerable.Repeat(42, Length).ToArray());
            Verify(buffer2.View, Enumerable.Repeat(23, Length).ToArray());
        }

        internal static void SwitchWithConstantConditionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var mode = 2;
            data[index] = mode switch
            {
                0 => 11,
                1 => 22,
                2 => 33,
                _ => 44
            };
        }

        [Fact]
        [KernelMethod(nameof(SwitchWithConstantConditionKernel))]
        public void SwitchWithConstantCondition()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);
            Verify(buffer.View, Enumerable.Repeat(33, Length).ToArray());
        }

        internal static void SwitchWithVariableConditionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> input,
            ArrayView1D<int, Stride1D.Dense> output)
        {
            output[index] = input[index] switch
            {
                0 => 11,
                1 => 22,
                2 => 33,
                _ => 44
            };
        }

        [Fact]
        [KernelMethod(nameof(SwitchWithVariableConditionKernel))]
        public void SwitchWithVariableCondition()
        {
            var inputArray = new[] { 0, 1, 2, 3, 4, 3, 2, 1, 0 };
            var expected = new[] { 11, 22, 33, 44, 44, 44, 33, 22, 11 };

            using var input = Accelerator.Allocate1D(inputArray);
            using var output = Accelerator.Allocate1D<int>(inputArray.Length);
            Execute(input.Length, input.View, output.View);
            Verify(output.View, expected);
        }
    }
}

#pragma warning restore CS0162
