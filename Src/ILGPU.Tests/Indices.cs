// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Indices.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
        [InlineData(1, 1_000_000, 1_000_000, 1, 0)]
        [InlineData(17, 10, 10, 7, 1)]
        [InlineData(6_000_000_005, 1_000_000, 1_000_000, 5, 6_000)]
        public void ReconstructIndex2_X(
            long linearIndex, int dimX, int dimY,
            int expectedX, int expectedY)
        {
            var extent = new Index2D(dimX, dimY);
            var index = Stride2D.DenseX.ReconstructFromElementIndex(linearIndex, extent);
            var backwards = Stride2D.DenseX.ComputeElementIndex(index, extent);
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
            Assert.Equal(linearIndex, backwards);
        }

        [Theory]
        [InlineData(1, 1_000_000, 1_000_000, 0, 1)]
        [InlineData(17, 10, 10, 1, 7)]
        [InlineData(6_000_000_005, 1_000_000, 1_000_000, 6_000, 5)]
        public void ReconstructIndex2_Y(
            long linearIndex, int dimX, int dimY,
            int expectedX, int expectedY)
        {
            var extent = new Index2D(dimX, dimY);
            var index = Stride2D.DenseY.ReconstructFromElementIndex(linearIndex, extent);
            var backwards = Stride2D.DenseY.ComputeElementIndex(index, extent);
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
            Assert.Equal(linearIndex, backwards);
        }

        [Theory]
        [InlineData(1, 10_000, 10_000, 10_000, 1, 0, 0)]
        [InlineData(217, 10, 10, 10, 7, 1, 2)]
        [InlineData(70_000_060_005, 10_000, 10_000, 10_000, 5, 6, 700)]
        public void ReconstructIndex3_XY(
            long linearIndex, int dimX, int dimY, int dimZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var extent = new Index3D(dimX, dimY, dimZ);
            var index = Stride3D.DenseXY.ReconstructFromElementIndex(linearIndex, extent);
            var backwards = Stride3D.DenseXY.ComputeElementIndex(index, extent);
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
            Assert.Equal(expectedZ, index.Z);
            Assert.Equal(linearIndex, backwards);
        }

        [Theory]
        [InlineData(1, 10_000, 10_000, 10_000, 0, 0, 1)]
        [InlineData(217, 10, 10, 10, 2, 1, 7)]
        [InlineData(70_000_060_005, 10_000, 10_000, 10_000, 700, 6, 5)]
        public void ReconstructIndex3_YZ(
            long linearIndex, int dimX, int dimY, int dimZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var extent = new Index3D(dimX, dimY, dimZ);
            var index = Stride3D.DenseZY.ReconstructFromElementIndex(linearIndex, extent);
            var backwards = Stride3D.DenseZY.ComputeElementIndex(index, extent);
            Assert.Equal(expectedX, index.X);
            Assert.Equal(expectedY, index.Y);
            Assert.Equal(expectedZ, index.Z);
            Assert.Equal(linearIndex, backwards);
        }
    }
}
