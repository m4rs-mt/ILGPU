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

        internal static void GridDimensionKernel(
            ArrayView1D<int, Stride1D.Dense> data)
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
                using var buffer = Accelerator.Allocate1D<int>(3);
                var extent = new KernelConfig(
                    new Index3D(
                        Math.Max(i * xMask, 1),
                        Math.Max(i * yMask, 1),
                        Math.Max(i * zMask, 1)),
                    Index3D.One);

                Execute(extent, buffer.View);

                var expected = new int[]
                {
                    extent.GridDim.X,
                    extent.GridDim.Y,
                    extent.GridDim.Z,
                };
                Verify(buffer.View, expected);
            }
        }

        internal static void GridLaunchDimensionKernel(
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[0] = Grid.DimX;
        }

        // This test is one-dimensional and uses small sizes for the sake of passing
        // tests on the CI machine, but on a machine with more threads it works
        // for higher dimensions and higher sizes.
        [Fact]
        public void GridLaunchDimension()
        {
            using var buffer = Accelerator.Allocate1D<int>(1);
            var kernel = Accelerator.LoadStreamKernel<ArrayView1D<int, Stride1D.Dense>>
                (GridLaunchDimensionKernel);

            kernel((1, 2), buffer.View);
            Accelerator.Synchronize();

            var data = buffer.GetAsArray1D();
            int expected = 1;

            Assert.Equal(expected, data[0]);
        }

        private static IEnumerable<Index1D> GetIndices1D(Accelerator accelerator)
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
            var maxBounds = new Index1D(int.MaxValue);
            static void AutoGroupedKernel1D(Index1D index, int _) { }

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

        private static IEnumerable<Index2D> GetIndices2D(Accelerator accelerator)
        {
            var x = (accelerator.MaxGridSize.X * accelerator.MaxNumThreadsPerGroup) + 1;
            var y = accelerator.MaxGridSize.Y + 1;
            yield return new Index2D(x, 1);
            yield return new Index2D(1, y);
            yield return new Index2D(x, y);
        }

        [Fact]
        public void GridLaunchDimensionOutOfRange2D()
        {
            const int UnusedParam = 0;
            var maxBounds = new Index2D(int.MaxValue, int.MaxValue);
            static void AutoGroupedKernel2D(Index2D index, int _) { }

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

        private static IEnumerable<Index3D> GetIndices3D(Accelerator accelerator)
        {
            var x = (accelerator.MaxGridSize.X * accelerator.MaxNumThreadsPerGroup) + 1;
            var y = accelerator.MaxGridSize.Y + 1;
            var z = accelerator.MaxGridSize.Z + 1;
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
            static void AutoGroupedKernel3D(Index3D index, int _) { }

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
