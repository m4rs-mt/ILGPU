using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class DebugTests : TestBase
    {
        protected DebugTests(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void DebugAssertKernel(
            Index index,
            ArrayView<int> data)
        {
            Debug.Assert(data[index] >= 0);
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
            Index index,
            ArrayView<int> data)
        {
            Debug.Assert(data[index] >= 0, "Invalid kernel argument");
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
            Index index,
            ArrayView<int> data)
        {
            if (data[index] < 0)
                Debug.Fail("Invalid kernel argument < 0");
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
