﻿// Enforce DEBUG mode in all cases to preserve Debug calls
#define DEBUG

using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class DebugTests : TestBase
    {
        protected DebugTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void DebugAssertKernel(
            Index1 index,
            ArrayView<int> data)
        {
            Debug.Assert(data[index] >= 0);
            Trace.Assert(data[index] >= 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(DebugAssertKernel))]
        public void DebugAssert(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Repeat(2, length).ToArray();
            buffer.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, expected.Length);
            Execute(length, buffer.View);
        }

        internal static void DebugAssertMessageKernel(
            Index1 index,
            ArrayView<int> data)
        {
            Debug.Assert(data[index] >= 0, "Invalid kernel argument");
            Trace.Assert(data[index] >= 0, "Invalid kernel argument");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(DebugAssertMessageKernel))]
        public void DebugAssertMessage(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Repeat(2, length).ToArray();
            buffer.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, expected.Length);
            Execute(length, buffer.View);
        }

        internal static void DebugFailedKernel(
            Index1 index,
            ArrayView<int> data)
        {
            if (data[index] < 0)
            {
                Debug.Fail("Invalid kernel argument < 0");
                Trace.Fail("Invalid kernel argument < 0");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(int.MaxValue >> 8 + 1)]
        [KernelMethod(nameof(DebugFailedKernel))]
        public void DebugFailed(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            var expected = Enumerable.Repeat(2, length).ToArray();
            buffer.CopyFrom(Accelerator.DefaultStream, expected, 0, 0, expected.Length);
            Execute(length, buffer.View);
        }
    }
}
