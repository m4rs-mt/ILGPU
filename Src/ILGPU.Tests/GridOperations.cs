using System;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    [Collection("DimensionOperations")]
    public abstract class GridOperations : TestBase
    {
        public GridOperations(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal static void GridDimensionKernel(
            GroupedIndex3 index,
            ArrayView<int> data)
        {
            data[0] = Grid.DimensionX;
            data[1] = Grid.DimensionY;
            data[2] = Grid.DimensionZ;
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
                using (var buffer = Accelerator.Allocate<int>(3))
                {
                    var extent = new GroupedIndex3(
                        new Index3(
                            Math.Max(i * xMask, 1),
                            Math.Max(i * yMask, 1),
                            Math.Max(i * zMask, 1)),
                        Index3.One);

                    Execute(extent, buffer.View);

                    var expected = new int[]
                    {
                        extent.GridIdx.X,
                        extent.GridIdx.Y,
                        extent.GridIdx.Z,
                    };
                    Verify(buffer, expected);
                }
            }
        }
    }
}
