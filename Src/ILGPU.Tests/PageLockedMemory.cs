using ILGPU.Runtime;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class PageLockedMemory : TestBase
    {
        protected PageLockedMemory(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        private const int Length = 1024;

        internal static void PinnedMemoryKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (data[index] == 0)
            {
                data[index] = 42;
            }
            else
            {
                data[index] = 24;
            }
        }

        [Fact]
        [KernelMethod(nameof(PinnedMemoryKernel))]
        public unsafe void PinnedUsingGCHandle()
        {
            var expected = Enumerable.Repeat(42, Length).ToArray();
            var array = new int[Length];
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                using var buffer = Accelerator.Allocate1D<int>(array.Length);
                using var scope = Accelerator.CreatePageLockFromPinned(array);

                buffer.View.CopyFromPageLockedAsync(scope);
                Execute(buffer.Length, buffer.View);

                buffer.View.CopyToPageLockedAsync(scope);
                Accelerator.Synchronize();
                Verify1D(array, expected);
            }
            finally
            {
                handle.Free();
            }
        }

#if NET5_0
        [Fact]
        [KernelMethod(nameof(PinnedMemoryKernel))]
        public void PinnedUsingGCAllocateArray()
        {
            var expected = Enumerable.Repeat(42, Length).ToArray();
            var array = System.GC.AllocateArray<int>(Length, pinned: true);
            using var buffer = Accelerator.Allocate1D<int>(array.Length);
            using var scope = Accelerator.CreatePageLockFromPinned(array);

            buffer.View.CopyFromPageLockedAsync(scope);
            Execute(buffer.Length, buffer.View);

            buffer.View.CopyToPageLockedAsync(scope);
            Accelerator.Synchronize();
            Verify1D(array, expected);
        }
#endif
    }
}
