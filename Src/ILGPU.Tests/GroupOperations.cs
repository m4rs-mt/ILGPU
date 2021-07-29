using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    [Collection("DimensionOperations")]
    public abstract class GroupOperations : TestBase
    {
        protected GroupOperations(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void GroupDimensionKernel(ArrayView1D<int, Stride1D.Dense> data)
        {
            data[0] = Group.DimX;
            data[1] = Group.DimY;
            data[2] = Group.DimZ;
        }

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(0, 1, 0)]
        [InlineData(0, 0, 1)]
        [KernelMethod(nameof(GroupDimensionKernel))]
        public void GroupDimension1D(int xMask, int yMask, int zMask)
        {
            for (int i = 2; i <= Math.Min(8, Accelerator.MaxNumThreadsPerGroup); i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(3);
                var extent = new KernelConfig(
                    new Index3D(1, 1, 1),
                    new Index3D(
                        Math.Max(i * xMask, 1),
                        Math.Max(i * yMask, 1),
                        Math.Max(i * zMask, 1)));

                Execute(extent, buffer.View);

                var expected = new int[]
                {
                    extent.GroupDim.X, extent.GroupDim.Y, extent.GroupDim.Z,
                };
                Verify(buffer.View, expected);
            }
        }

        [Theory]
        [InlineData(1, 1, 0)]
        [InlineData(0, 1, 1)]
        [InlineData(1, 0, 1)]
        [KernelMethod(nameof(GroupDimensionKernel))]
        public void GroupDimension2D(int xMask, int yMask, int zMask)
        {
            var end = (int)Math.Sqrt(Accelerator.MaxNumThreadsPerGroup);
            for (int i = 2; i <= end; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(3);
                var extent = new KernelConfig(
                    new Index3D(1, 1, 1),
                    new Index3D(
                        Math.Max(i * xMask, 1),
                        Math.Max(i * yMask, 1),
                        Math.Max(i * zMask, 1)));
                Execute(extent, buffer.View);

                var expected = new int[]
                {
                    extent.GroupDim.X, extent.GroupDim.Y, extent.GroupDim.Z,
                };
                Verify(buffer.View, expected);
            }
        }

        [Fact]
        [KernelMethod(nameof(GroupDimensionKernel))]
        public void GroupDimension3D()
        {
            var end = (int)Math.Pow(Accelerator.MaxNumThreadsPerGroup, 1.0 / 3.0);
            for (int i = 1; i <= end; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(3);
                var extent = new KernelConfig(
                    new Index3D(1, 1, 1),
                    new Index3D(i, i, i));
                Execute(extent, buffer.View);

                var expected = new int[]
                {
                    extent.GroupDim.X, extent.GroupDim.Y, extent.GroupDim.Z,
                };
                Verify(buffer.View, expected);
            }
        }

        internal static void GroupBarrierKernel(ArrayView1D<int, Stride1D.Dense> data)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;
            Group.Barrier();
            data[idx] = idx;
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupBarrierKernel))]
        public void GroupBarrier(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            for (int i = 1; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                var extent = new KernelConfig(
                    length,
                    i);
                Execute(extent, buffer.View);

                var expected = Enumerable.Range(0, length * i).ToArray();
                Verify(buffer.View, expected);
            }
        }

        internal static void GroupBarrierAndKernel(
            ArrayView1D<int, Stride1D.Dense> data,
            Index1D bound)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;
            data[idx] = Group.BarrierAnd(Group.IdxX < bound) ? 1 : 0;
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupBarrierAndKernel))]
        public void GroupBarrierAnd(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            for (int i = 2; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                var extent = new KernelConfig(length, i);
                Execute(extent, buffer.View, new Index1D(i));

                var expected = Enumerable.Repeat(1, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);

                Execute(extent, buffer.View, new Index1D(i - 1));

                expected = Enumerable.Repeat(0, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);
            }
        }

        internal static void GroupBarrierOrKernel(
            ArrayView1D<int, Stride1D.Dense> data,
            Index1D bound)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;
            data[idx] = Group.BarrierOr(Group.IdxX < bound) ? 1 : 0;
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupBarrierOrKernel))]
        public void GroupBarrierOr(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            for (int i = 2; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                var extent = new KernelConfig(length, i);
                Execute(extent, buffer.View, new Index1D(1));

                var expected = Enumerable.Repeat(1, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);

                Execute(extent, buffer.View, new Index1D(0));

                expected = Enumerable.Repeat(0, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);
            }
        }

        internal static void GroupBarrierPopCountKernel(
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> data2,
            Index1D bound)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;
            data[idx] = Group.BarrierPopCount(Group.IdxX < bound);
            data2[idx] = Group.BarrierPopCount(Group.IdxX >= bound);
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupBarrierPopCountKernel))]
        public void GroupBarrierPopCount(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            for (int i = 2; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                using var buffer2 = Accelerator.Allocate1D<int>(length * i);
                var extent = new KernelConfig(length, i);
                Execute(extent, buffer.View, buffer2.View, new Index1D(i));

                var expected = Enumerable.Repeat(i, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);

                var expected2 = Enumerable.Repeat(0, (int)buffer.Length).ToArray();
                Verify(buffer2.View, expected2);
            }
        }

        internal static void GroupBroadcastKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;
            data[idx] = Group.Broadcast(Group.IdxX, Group.DimX - 1);
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupBroadcastKernel))]
        public void GroupBroadcast(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            for (int i = 2; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                var extent = new KernelConfig(length, i);
                Execute(extent, buffer.View);

                var expected = Enumerable.Repeat(i - 1, (int)buffer.Length).ToArray();
                Verify(buffer.View, expected);
            }
        }

        internal static void GroupDivergentControlFlowKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var idx = Grid.IdxX * Group.DimX + Group.IdxX;

            Group.Barrier();
            for (var i = 0; i < Group.IdxX; i++)
            {
                Group.Barrier();
                Atomic.Add(ref data[idx], 1);
            }
        }

        [SkippableTheory]
        [InlineData(32)]
        [InlineData(256)]
        [InlineData(1024)]
        [KernelMethod(nameof(GroupDivergentControlFlowKernel))]
        public void GroupDivergentControlFlow(int length)
        {
            Skip.If(length > Accelerator.MaxNumThreadsPerGroup);

            // IMPORTANT: Iteration range has been limited to the warp size of the
            // accelerator.
            //
            // Some OpenCL drivers have been known to deadlock when the group dimensions
            // are larger than the warp size. It is also important to use the latest
            // drivers.
            //
            // e.g. Intel HD Graphics 630 drivers for OpenCL v1.2, with WarpSize = 16
            //  v21.20.16.4550 deadlocks when group dimensions are larger than 8
            //  v26.20.100.7263 deadlocks when group dimensions are larger than 16
            //
            for (int i = 2; i <= Accelerator.WarpSize; i <<= 1)
            {
                using var buffer = Accelerator.Allocate1D<int>(length * i);
                buffer.MemSetToZero();
                Accelerator.Synchronize();

                var extent = new KernelConfig(length, i);
                Execute(extent, buffer.View);

                var expected = Enumerable.Repeat(Enumerable.Range(0, i), length)
                    .SelectMany(x => x).ToArray();
                Verify(buffer.View, expected);
            }
        }

        private static IEnumerable<Index1D> GetIndices1D(Accelerator accelerator)
        {
            yield return accelerator.MaxGroupSize.X + 1;
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange1D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index1D(int.MaxValue);
            static void Kernel1D(int _) { }

            foreach (var index in GetIndices1D(Accelerator))
            {
                if (index.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.Launch(
                            Kernel1D,
                            new KernelConfig(Index1D.One, index),
                            UnusedParam));
                }
            }
        }

        private static IEnumerable<Index2D> GetIndices2D(Accelerator accelerator)
        {
            var x = accelerator.MaxGroupSize.X + 1;
            var y = accelerator.MaxGroupSize.Y + 1;
            yield return new Index2D(x, 1);
            yield return new Index2D(1, y);
            yield return new Index2D(x, y);
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange2D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index2D(int.MaxValue, int.MaxValue);
            static void Kernel2D(int _) { }

            foreach (var index2 in GetIndices2D(Accelerator))
            {
                if (index2.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.Launch(
                            Kernel2D,
                            new KernelConfig(Index2D.One, index2),
                            UnusedParam));
                }
            }
        }

        private static IEnumerable<Index3D> GetIndices3D(Accelerator accelerator)
        {
            var x = accelerator.MaxGroupSize.X + 1;
            var y = accelerator.MaxGroupSize.Y + 1;
            var z = accelerator.MaxGroupSize.Z + 1;
            yield return new Index3D(x, 1, 1);
            yield return new Index3D(1, y, 1);
            yield return new Index3D(x, y, 1);
            yield return new Index3D(1, 1, z);
            yield return new Index3D(x, 1, z);
            yield return new Index3D(1, y, z);
            yield return new Index3D(x, y, z);
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange3D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index3D(int.MaxValue, int.MaxValue, int.MaxValue);
            static void Kernel3D(int _) { }

            foreach (var index3 in GetIndices3D(Accelerator))
            {
                if (index3.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.Launch(
                            Kernel3D,
                            new KernelConfig(Index3D.One, index3),
                            UnusedParam));
                }
            }
        }
    }
}
