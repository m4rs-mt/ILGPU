﻿using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class MemoryFenceOperations : TestBase
    {
        private const int Length = 1024;

        public MemoryFenceOperations(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void MemoryFenceGroupLevelKernel(
            GroupedIndex index,
            ArrayView<int> data)
        {
            var idx = index.ComputeGlobalIndex();
            data[idx] = idx;

            MemoryFence.GroupLevel();
        }

        [Fact]
        [KernelMethod(nameof(MemoryFenceGroupLevelKernel))]
        public void MemoryFenceGroupLevel()
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                var extent = new GroupedIndex(Length, i);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                Execute(extent, buffer.View);

                var expected = Enumerable.Range(0, extent.Size).ToArray();
                Verify(buffer, expected);
            }
        }

        internal static void MemoryFenceDeviceLevelKernel(
            Index index,
            ArrayView<int> data)
        {
            data[index] = index;

            MemoryFence.DeviceLevel();
        }

        [Fact]
        [KernelMethod(nameof(MemoryFenceDeviceLevelKernel))]
        public void MemoryFenceDeviceLevel()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(Length, buffer.View);

            var expected = Enumerable.Range(0, Length).ToArray();
            Verify(buffer, expected);
        }

        internal static void MemoryFenceSystemLevelKernel(
            Index index,
            ArrayView<int> data)
        {
            data[index] = index;

            MemoryFence.SystemLevel();
        }

        [Fact]
        [KernelMethod(nameof(MemoryFenceSystemLevelKernel))]
        public void MemoryFenceSystemLevel()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(Length, buffer.View);

            var expected = Enumerable.Range(0, Length).ToArray();
            Verify(buffer, expected);
        }
    }
}
