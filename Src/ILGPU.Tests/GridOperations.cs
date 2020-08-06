using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using System;
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
    }
}
