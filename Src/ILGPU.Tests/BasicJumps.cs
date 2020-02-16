using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

namespace ILGPU.Tests
{
    public abstract class BasicJumps : TestBase
    {
        protected BasicJumps(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Testing unconditional jump")]
        internal static void BasicJumpKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            var value = source[index];
            goto exit;

            data[index] = value;
            return;

            exit:
            data[index] = 23;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(1024)]
        [KernelMethod(nameof(BasicJumpKernel))]
        public void BasicJump(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var sourceData = Enumerable.Repeat(42, buffer.Length).ToArray();
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, buffer.Length);

            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(23, buffer.Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void BasicIfJumpKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            var value = source[index];
            if (value < 23)
                goto exit;

            data[index] = value;
            return;

            exit:
            data[index] = 23;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(1024)]
        [KernelMethod(nameof(BasicIfJumpKernel))]
        public void BasicIfJump(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(length);
            var partLength = source.Length / 3;
            var sourceData = Enumerable.Repeat(13, partLength).Concat(
                Enumerable.Repeat(42, source.Length - partLength)).ToArray();
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, source.Length);

            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(23, partLength).Concat(
                Enumerable.Repeat(42, length - partLength)).ToArray();
            Verify(buffer, expected);
        }

        internal static void BasicLoopJumpKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] == 23)
                    goto exit;
            }

            data[index] = 42;
            return;

            exit:
            data[index] = 23;
        }

        [Theory]
        [InlineData(32)]
        [InlineData(1024)]
        [KernelMethod(nameof(BasicLoopJumpKernel))]
        public void BasicLoopJump(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(64);
            var sourceData = Enumerable.Range(0, source.Length).ToArray();
            sourceData[57] = 23;
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, source.Length);

            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(23, length).ToArray();
            Verify(buffer, expected);
        }
    }
}

#pragma warning restore CS0162
