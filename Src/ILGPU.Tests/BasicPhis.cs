using ILGPU.Runtime;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class BasicPhis : TestBase
    {
        private const int Length = 32;

        protected BasicPhis(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetValueLoop(int c)
        {
            int result = 0;
            for (int i = c; i < 23; ++i)
                result = 1;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetValueLoop2(int c)
        {
            int result = 1;
            for (int i = c; i >= 0; --i)
                result <<= 1;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetValue(int c)
        {
            int result = 0;
            if (c < 23)
                result = GetValueLoop(c + 1);
            else
                result += GetValueLoop2(c / 2);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteZero(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = index;
        }

        internal static void PhiInliningKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            int c)
        {
            WriteZero(index, data);
            int value = GetValueLoop2(c);
            data[index] = value;
        }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(23, 0x1000000)]
        [KernelMethod(nameof(PhiInliningKernel))]
        public void PhiInlining(int c, int res)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, c);

            var expected = Enumerable.Repeat(res, Length).ToArray();
            Verify(buffer.View, expected);
        }

        internal static void PhiInliningDeepKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            int c)
        {
            WriteZero(index, data);
            int value = GetValue(c);
            data[index] = value;
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(23, 4096)]
        [KernelMethod(nameof(PhiInliningDeepKernel))]
        public void PhiInliningDeep(int c, int res)
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, c);

            var expected = Enumerable.Repeat(res, Length).ToArray();
            Verify(buffer.View, expected);
        }
    }
}
