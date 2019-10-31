using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class EntryPoints : TestBase
    {
        public EntryPoints(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void Index1EntryPointKernel(Index index, ArrayView<int> output)
        {
            output[index] = index;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        [KernelMethod(nameof(Index1EntryPointKernel))]
        public void Index1EntryPoint(int length)
        {
            using var buffer = Accelerator.Allocate<int>(length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Range(0, length).ToArray();
            Verify(buffer, expected);
        }

        internal static void Index2EntryPointKernel(
            Index2 index,
            ArrayView<int> output,
            Index2 extent)
        {
            var linearIndex = index.ComputeLinearIndex(extent);
            output[linearIndex] = linearIndex;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(257)]
        [InlineData(513)]
        [KernelMethod(nameof(Index2EntryPointKernel))]
        public void Index2EntryPoint(int length)
        {
            var extent = new Index2(length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        internal static void Index3EntryPointKernel(
            Index3 index,
            ArrayView<int> output,
            Index3 extent)
        {
            var linearIndex = index.ComputeLinearIndex(extent);
            output[linearIndex] = linearIndex;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(257)]
        [KernelMethod(nameof(Index3EntryPointKernel))]
        public void Index3EntryPoint(int length)
        {
            var extent = new Index3(length, length, length);
            using var buffer = Accelerator.Allocate<int>(extent.Size);
            Execute(extent, buffer.View, extent);

            var expected = Enumerable.Range(0, extent.Size).ToArray();
            Verify(buffer, expected);
        }

        internal static void GroupedIndex1EntryPointKernel(
            GroupedIndex index, ArrayView<int> output, int stride)
        {
            var idx = index.GridIdx.X * stride + index.GroupIdx.X;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(513)]
        [InlineData(1025)]
        [KernelMethod(nameof(GroupedIndex1EntryPointKernel))]
        public void GroupedIndex1EntryPoint(int length)
        {
            for (int i = 1; i < Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                var extent = new GroupedIndex(length, i);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                Execute(extent, buffer.View, i);

                var expected = new int[extent.Size];
                for (int j = 0; j < length; ++j)
                {
                    for (int k = 0; k < i; ++k)
                    {
                        var idx = j * i + k;
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }

        internal static void GroupedIndex2EntryPointKernel(
            GroupedIndex2 index, ArrayView<int> output, Index2 stride, Index2 extent)
        {
            var idx1 = index.GridIdx.X * stride.X + index.GroupIdx.X;
            var idx2 = index.GridIdx.Y * stride.Y + index.GroupIdx.Y;
            var idx = idx2 * extent.X + idx1;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [InlineData(65)]
        [InlineData(129)]
        [KernelMethod(nameof(GroupedIndex2EntryPointKernel))]
        public void GroupedIndex2EntryPoint(int length)
        {
            var end = (int)Math.Sqrt(Accelerator.MaxNumThreadsPerGroup);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index2(i, i);
                var extent = new GroupedIndex2(
                    new Index2(length, length),
                    stride);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridIdx);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length; ++j)
                {
                    var gridIdx = Index2.ReconstructIndex(j, extent.GridIdx);
                    for (int k = 0; k < i * i; ++k)
                    {
                        var groupIdx = Index2.ReconstructIndex(k, extent.GroupIdx);
                        var idx = (gridIdx * stride + groupIdx).ComputeLinearIndex(extent.GridIdx);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }

        internal static void GroupedIndex3EntryPointKernel(
            GroupedIndex3 index, ArrayView<int> output, Index3 stride, Index3 extent)
        {
            var idx1 = index.GridIdx.X * stride.X + index.GroupIdx.X;
            var idx2 = index.GridIdx.Y * stride.Y + index.GroupIdx.Y;
            var idx3 = index.GridIdx.Z * stride.Z + index.GroupIdx.Z;
            var idx = ((idx3 * extent.Y) + idx2) * extent.X + idx1;
            output[idx] = idx;
        }

        [Theory]
        [InlineData(33)]
        [KernelMethod(nameof(GroupedIndex3EntryPointKernel))]
        public void GroupedIndex3EntryPoint(int length)
        {
            var end = (int)Math.Pow(Accelerator.MaxNumThreadsPerGroup, 1.0 / 3.0);
            for (int i = 1; i <= end; i <<= 1)
            {
                var stride = new Index3(i, i, i);
                var extent = new GroupedIndex3(
                    new Index3(length, length, length),
                    stride);
                using var buffer = Accelerator.Allocate<int>(extent.Size);
                buffer.MemSetToZero(Accelerator.DefaultStream);
                Execute(extent, buffer.View, stride, extent.GridIdx);

                var expected = new int[extent.Size];
                for (int j = 0; j < length * length * length; ++j)
                {
                    var gridIdx = Index3.ReconstructIndex(j, extent.GridIdx);
                    for (int k = 0; k < i * i * i; ++k)
                    {
                        var groupIdx = Index3.ReconstructIndex(k, extent.GroupIdx);
                        var idx = (gridIdx * stride + groupIdx).ComputeLinearIndex(extent.GridIdx);
                        expected[idx] = idx;
                    }
                }

                Verify(buffer, expected);
            }
        }
    }
}
