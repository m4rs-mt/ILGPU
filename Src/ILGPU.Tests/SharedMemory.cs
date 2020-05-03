using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class SharedMemory : TestBase
    {
        protected SharedMemory(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void SharedMemoryVariableKernel(ArrayView<int> data)
        {
            ref var sharedMemory = ref ILGPU.SharedMemory.Allocate<int>();
            if (Group.IsFirstThread)
                sharedMemory = 0;
            Group.Barrier();

            Atomic.Add(ref sharedMemory, 1);
            Group.Barrier();

            var idx = Grid.GlobalIndex.X;
            data[idx] = sharedMemory;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [KernelMethod(nameof(SharedMemoryVariableKernel))]
        public void SharedMemoryVariable(int groupMultiplier)
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate<int>(i * groupMultiplier);
                var index = new KernelConfig(groupMultiplier, i);
                Execute(index, buffer.View);

                var expected = Enumerable.Repeat(i, buffer.Length).ToArray();
                Verify(buffer, expected);
            }
        }

        internal static void SharedMemoryArrayKernel(ArrayView<int> data)
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate<int>(2);
            sharedMemory[Group.IdxX] = Group.IdxX;
            Group.Barrier();

            var idx = Grid.GlobalIndex.X;
            data[idx] = sharedMemory[(Group.IdxX + 1) % Group.DimX];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(32)]
        [KernelMethod(nameof(SharedMemoryArrayKernel))]
        public void SharedMemoryArray(int groupMultiplier)
        {
            using var buffer = Accelerator.Allocate<int>(2 * groupMultiplier);
            var index = new KernelConfig(groupMultiplier, 2);
            Execute(index, buffer.View);

            var expected = new int[buffer.Length];
            for (int i = 0; i < buffer.Length; i += 2)
            {
                expected[i] = 1;
                expected[i + 1] = 0;
            }

            Verify(buffer, expected);
        }
    }
}
