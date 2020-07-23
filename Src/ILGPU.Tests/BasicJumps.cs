﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

namespace ILGPU.Tests
{
    public abstract class BasicJumps : TestBase
    {
        protected BasicJumps(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [SuppressMessage(
            "Style",
            "IDE0059:Unnecessary assignment of a value",
            Justification = "Testing unconditional jump")]
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
            var sourceData = Enumerable.Repeat(42, (int)buffer.Length).ToArray();
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, buffer.Length);

            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(23, (int)buffer.Length).ToArray();
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
            var partLength = (int)source.Length / 3;
            var sourceData = Enumerable.Repeat(13, partLength).Concat(
                Enumerable.Repeat(42, (int)source.Length - partLength)).ToArray();
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
            var sourceData = Enumerable.Range(0, (int)source.Length).ToArray();
            sourceData[57] = 23;
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, source.Length);

            Execute(buffer.Length, buffer.View, source.View);

            var expected = Enumerable.Repeat(23, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void BasicNestedLoopJumpKernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> source,
            int c)
        {
            int k = 0;
        entry:
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] == 23)
                {
                    if (k < c)
                        goto exit;
                    goto nested;
                }
            }

            data[index] = 42;
            return;

        nested:
            k = 43;

        exit:
            if (k++ < 1)
                goto entry;
            data[index] = 23 + k;
        }

        [Theory]
        [InlineData(32, 0, 67)]
        [InlineData(32, 2, 25)]
        [InlineData(1024, 0, 67)]
        [InlineData(1024, 2, 25)]
        [KernelMethod(nameof(BasicNestedLoopJumpKernel))]
        public void BasicNestedLoopJump(int length, int c, int res)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            using var source = Accelerator.Allocate<int>(64);
            var sourceData = Enumerable.Range(0, (int)source.Length).ToArray();
            sourceData[57] = 23;
            source.CopyFrom(Accelerator.DefaultStream, sourceData, 0, 0, source.Length);

            Execute(buffer.Length, buffer.View, source.View, c);

            var expected = Enumerable.Repeat(res, length).ToArray();
            Verify(buffer, expected);
        }
    }
}

#pragma warning restore CS0162
