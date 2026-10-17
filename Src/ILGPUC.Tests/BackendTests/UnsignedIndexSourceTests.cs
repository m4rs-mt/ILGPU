// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedIndexSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that unsigned 32-bit element offsets widened to long are zero-extended
/// in the element addresses of GPU sources.
/// </summary>
public sealed class UnsignedIndexSourceTests
{
    /// <summary>
    /// Offsets a reference by an unsigned 32-bit offset widened to long, which
    /// reaches beyond 2^31 elements in buffers of that size.
    /// </summary>
    internal static void UnsignedElementOffsetKernel(
        Index1D index,
        ArrayView1D<byte, Stride1D.Dense> data,
        ArrayView1D<uint, Stride1D.Dense> offsets)
    {
        Unsafe.Add(ref data[0], (nint)(long)offsets[index]) = 1;
    }

    /// <summary>
    /// The element address adds the zero-extended offset instead of the 32-bit
    /// offset, which would be sign-extended.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Metal, "(ulong)(uint)tmp_")]
    [InlineData(BackendType.OpenCL, "(ulong)(uint)tmp_")]
    [InlineData(BackendType.Cuda, "(unsigned long long)(unsigned int)tmp_")]
    [InlineData(BackendType.ROCm, "(unsigned long long)(unsigned int)tmp_")]
    public void UnsignedElementOffset_IsZeroExtended(
        BackendType backend,
        string zeroExtension)
    {
        var kernel = typeof(UnsignedIndexSourceTests).GetMethod(
            nameof(UnsignedElementOffsetKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);

        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        Assert.Contains(zeroExtension, source);
    }
}
