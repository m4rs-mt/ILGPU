using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class SharedMemory : TestBase
    {
        public SharedMemory(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void SharedMemoryVariableKernel(
            GroupedIndex index,
            ArrayView<int> data)
        {
            ref var sharedMemory = ref ILGPU.SharedMemory.Allocate<int>();
            if (index.GroupIdx.IsFirst)
                sharedMemory = 0;
            Group.Barrier();

            Atomic.Add(ref sharedMemory, 1);
            Group.Barrier();

            var idx = index.ComputeGlobalIndex();
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
                using (var buffer = Accelerator.Allocate<int>(i * groupMultiplier))
                {
                    var index = new GroupedIndex(groupMultiplier, i);
                    Execute(index, buffer.View);

                    var expected = Enumerable.Repeat(i, buffer.Length).ToArray();
                    Verify(buffer, expected);
                }
            }
        }

        internal static void SharedMemoryArrayKernel(
            GroupedIndex index,
            ArrayView<int> data)
        {
            var sharedMemory = ILGPU.SharedMemory.Allocate<int>(2);
            sharedMemory[index.GroupIdx] = index.GroupIdx;
            Group.Barrier();

            var idx = index.ComputeGlobalIndex();
            data[idx] = sharedMemory[(index.GroupIdx + 1) % Group.DimensionX];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(32)]
        [KernelMethod(nameof(SharedMemoryArrayKernel))]
        public void SharedMemoryArray(int groupMultiplier)
        {
            using (var buffer = Accelerator.Allocate<int>(2 * groupMultiplier))
            {
                var index = new GroupedIndex(groupMultiplier, 2);
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
}
