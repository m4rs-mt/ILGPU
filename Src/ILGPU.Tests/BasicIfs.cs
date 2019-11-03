using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

namespace ILGPU.Tests
{
    public abstract class BasicIfs : TestBase
    {
        private const int Length = 32;

        protected BasicIfs(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void IfTrueKernel(
            Index index,
            ArrayView<int> data)
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
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void IfFalseKernel(
            Index index,
            ArrayView<int> data)
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
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void IfTrueSideEffectsKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> data2)
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
            using var buffer = Accelerator.Allocate<int>(Length);
            using var buffer2 = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);

            var expected2 = Enumerable.Repeat(1, Length).ToArray();
            Verify(buffer2, expected2);
        }

        internal static void IfFalseSideEffectsKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> data2)
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
            using var buffer = Accelerator.Allocate<int>(Length);
            using var buffer2 = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer, expected);

            var expected2 = Enumerable.Repeat(0, Length).ToArray();
            Verify(buffer2, expected2);
        }

        internal static void IfSideEffectsKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> data2,
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
            using var buffer = Accelerator.Allocate<int>(Length);
            using var buffer2 = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View, c);

            var expected = Enumerable.Repeat(res1, Length).ToArray();
            Verify(buffer, expected);

            var expected2 = Enumerable.Repeat(res2, Length).ToArray();
            Verify(buffer2, expected2);
        }

        internal static void IfNestedSideEffectsKernel(
            Index index,
            ArrayView<int> data,
            ArrayView<int> data2,
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
            using var buffer = Accelerator.Allocate<int>(Length);
            using var buffer2 = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, buffer2.View, c, d);

            var expected = Enumerable.Repeat(res1, Length).ToArray();
            Verify(buffer, expected);

            var expected2 = Enumerable.Repeat(res2, Length).ToArray();
            Verify(buffer2, expected2);
        }

        internal static void IfAndOrKernel(
            Index index,
            ArrayView<int> data)
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
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, 2).Concat(Enumerable.Repeat(0, Length - 2)).ToArray();
            Verify(buffer, expected);
        }
    }
}

#pragma warning restore CS0162
