using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class Indices : TestBase
    {
        protected Indices(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        public void ReconstructIndex1(long linearIndex)
        {
            var index = Index1.ReconstructIndex(linearIndex, int.MaxValue);
            Assert.Equal(linearIndex, index.Size);
        }

        [Theory]
        [InlineData(1, 1_000_000, 1_000_000, 1, 0)]
        [InlineData(17, 10, 10, 7, 1)]
        [InlineData(6_000_000_005, 1_000_000, 1_000_000,
            5, 6_000)]  // Make sure linearIndices > int.MaxValue work.
        public void ReconstructIndex2(
            long linearIndex, int dimX, int dimY,
            int expectedX, int expectedY)
        {
            var index = Index2.ReconstructIndex(linearIndex, new Index2(dimX, dimY));
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
        }

        [Theory]
        [InlineData(1, 1_000_000, 1_000_000, 1_000_000, 1, 0, 0)]
        [InlineData(217, 10, 10, 10, 7, 1, 2)]
        [InlineData(
            70_000_060_005, 10_000, 10_000, 10_000,
            5, 6, 700)]  // Make sure linearIndices > int.MaxValue work.
        [InlineData(
            700_000_000_006_000_005, 1_000_000, 1_000_000, 1_000_000,
            5, 6, 700_000)]  // yz > int.MaxValue
        public void ReconstructIndex3(
            long linearIndex, int dimX, int dimY, int dimZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var index = Index3.ReconstructIndex(
                linearIndex,
                new Index3(dimX, dimY, dimZ));
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
            Assert.Equal(expectedZ, index.Z);
        }
    }
}
