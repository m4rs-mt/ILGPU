using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    [Collection("DimensionOperations")]
    public abstract class GridOperations : TestBase
    {
        protected GridOperations(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void GridDimensionKernel(ArrayView<int> data)
        {
            data[0] = Grid.DimX;
            data[1] = Grid.DimY;
            data[2] = Grid.DimZ;

            Debug.Assert(Grid.IdxX < Grid.DimX);
            Debug.Assert(Grid.IdxY < Grid.DimY);
            Debug.Assert(Grid.IdxZ < Grid.DimZ);
        }

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(0, 1, 0)]
        [InlineData(0, 0, 1)]
        [KernelMethod(nameof(GridDimensionKernel))]
        public void GridDimension(int xMask, int yMask, int zMask)
        {
            for (int i = 2; i <= Accelerator.MaxNumThreadsPerGroup; i <<= 1)
            {
                using var buffer = Accelerator.Allocate<int>(3);
                var extent = new KernelConfig(
                    new Index3(
                        Math.Max(i * xMask, 1),
                        Math.Max(i * yMask, 1),
                        Math.Max(i * zMask, 1)),
                    Index3.One);

                Execute(extent, buffer.View);

                var expected = new int[]
                {
                    extent.GridDim.X,
                    extent.GridDim.Y,
                    extent.GridDim.Z,
                };
                Verify(buffer, expected);
            }
        }

        internal static void GridLaunchDimensionKernel(ArrayView<int> data)
        {
            data[0] = Grid.DimX;
        }

        // This test is one-dimensional and uses small sizes for the sake of passing
        // tests on the CI machine, but on a machine with more threads it works
        // for higher dimensions and higher sizes.
        [Fact]
        public void GridLaunchDimension()
        {
            using var buffer = Accelerator.Allocate<int>(1);
            var kernel = Accelerator.LoadStreamKernel<ArrayView<int>>
                (GridLaunchDimensionKernel);

            kernel((1, 2), buffer.View);
            Accelerator.Synchronize();

            var data = buffer.GetAsArray();
            int expected = 1;

            Assert.Equal(expected, data[0]);
        }

        private static IEnumerable<Index1> GetIndices1D(Accelerator accelerator)
        {
            if (accelerator.MaxGridSize.X < int.MaxValue)
            {
                var x = accelerator.MaxGridSize.X * accelerator.MaxNumThreadsPerGroup + 1;
                yield return x;
            }
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange1D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index1(int.MaxValue);
            static void AutoGroupedKernel1D(Index1 index, int _) { }

            foreach (var index in GetIndices1D(Accelerator))
            {
                if (index.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.LaunchAutoGrouped(
                            AutoGroupedKernel1D,
                            index,
                            UnusedParam));
                }
            }
        }

        private static IEnumerable<Index2> GetIndices2D(Accelerator accelerator)
        {
            var x = (accelerator.MaxGridSize.X * accelerator.MaxNumThreadsPerGroup) + 1;
            var y = accelerator.MaxGridSize.Y + 1;
            yield return new Index2(x, 1);
            yield return new Index2(1, y);
            yield return new Index2(x, y);
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange2D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index2(int.MaxValue, int.MaxValue);
            static void AutoGroupedKernel2D(Index2 index, int _) { }

            foreach (var index2 in GetIndices2D(Accelerator))
            {
                if (index2.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.LaunchAutoGrouped(
                            AutoGroupedKernel2D,
                            index2,
                            UnusedParam));
                }
            }
        }

        private static IEnumerable<Index3> GetIndices3D(Accelerator accelerator)
        {
            var x = (accelerator.MaxGridSize.X * accelerator.MaxNumThreadsPerGroup) + 1;
            var y = accelerator.MaxGridSize.Y + 1;
            var z = accelerator.MaxGridSize.Z + 1;
            yield return new Index3(x, 1, 1);
            yield return new Index3(1, y, 1);
            yield return new Index3(x, y, 1);
            yield return new Index3(1, 1, z);
            yield return new Index3(x, 1, z);
            yield return new Index3(1, y, z);
            yield return new Index3(x, y, z);
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange3D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index3(int.MaxValue, int.MaxValue, int.MaxValue);
            static void AutoGroupedKernel3D(Index3 index, int _) { }

            foreach (var index3 in GetIndices3D(Accelerator))
            {
                if (index3.InBoundsInclusive(maxBounds))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        Accelerator.LaunchAutoGrouped(
                            AutoGroupedKernel3D,
                            index3,
                            UnusedParam));
                }
            }
        }
    }
}
