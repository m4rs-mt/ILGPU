// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SizeOfTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.Tests.Framework;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

public sealed class SizeOfTests
{
    [Fact]
    public void SizeOf_Byte() =>
        Assert.Equal(1, Interop.SizeOf<byte>());

    [Fact]
    public void SizeOf_Int16() =>
        Assert.Equal(2, Interop.SizeOf<short>());

    [Fact]
    public void SizeOf_Int32() =>
        Assert.Equal(4, Interop.SizeOf<int>());

    [Fact]
    public void SizeOf_Int64() =>
        Assert.Equal(8, Interop.SizeOf<long>());

    [Fact]
    public void SizeOf_Float() =>
        Assert.Equal(4, Interop.SizeOf<float>());

    [Fact]
    public void SizeOf_Double() =>
        Assert.Equal(8, Interop.SizeOf<double>());

    [Fact]
    public void SizeOf_TestStruct()
    {
        var expected = Unsafe.SizeOf<TestStruct>();
        Assert.Equal(expected, Interop.SizeOf<TestStruct>());
    }

    [Fact]
    public void SizeOf_PairStruct_Int()
    {
        var expected = Unsafe.SizeOf<PairStruct<int>>();
        Assert.Equal(expected, Interop.SizeOf<PairStruct<int>>());
    }
}
