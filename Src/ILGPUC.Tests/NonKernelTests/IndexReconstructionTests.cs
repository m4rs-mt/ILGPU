// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IndexReconstructionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

public sealed class IndexReconstructionTests
{
    [Theory]
    [InlineData(0, 10, 0, 0)]
    [InlineData(5, 10, 5, 0)]
    [InlineData(10, 10, 0, 1)]
    [InlineData(15, 10, 5, 1)]
    public void Reconstruct2D_FromLinear(int linear, int width, int expectedX, int expectedY)
    {
        var x = linear % width;
        var y = linear / width;
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Theory]
    [InlineData(0, 10, 10, 0, 0, 0)]
    [InlineData(5, 10, 10, 5, 0, 0)]
    [InlineData(15, 10, 10, 5, 1, 0)]
    [InlineData(105, 10, 10, 5, 0, 1)]
    public void Reconstruct3D_FromLinear(
        int linear, int width, int height,
        int expectedX, int expectedY, int expectedZ)
    {
        var x = linear % width;
        var y = (linear / width) % height;
        var z = linear / (width * height);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Fact]
    public void Index1D_RoundTrip()
    {
        var idx = new Index1D(42);
        Assert.Equal(42, idx.X);
    }

    [Fact]
    public void Index2D_Construction()
    {
        var idx = new Index2D(3, 7);
        Assert.Equal(3, idx.X);
        Assert.Equal(7, idx.Y);
    }

    [Fact]
    public void Index3D_Construction()
    {
        var idx = new Index3D(2, 5, 8);
        Assert.Equal(2, idx.X);
        Assert.Equal(5, idx.Y);
        Assert.Equal(8, idx.Z);
    }
}
