// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks of generated CUDA C++ source for constructs nvcc only warns about,
/// which the tests would otherwise never see.
/// </summary>
public sealed class CudaSourceTests
{
    /// <summary>
    /// CUDA C++ pointer types carry no address space: <c>__shared__</c> in a
    /// cast type is an attribute that does not apply there (nvcc warning
    /// 1835-D), so casts into shared memory use plain pointer types.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    public void SharedPointerCasts_HaveNoStorageKeywords(BackendType backend)
    {
        var kernel = typeof(SharedMemoryKernels)
            .GetMethod(nameof(SharedMemoryKernels.SharedFieldCastKernel))!;
        using var helper = new CompilationHelper(backend);
        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;

        Assert.Contains("__shared__", source);
        Assert.DoesNotContain("(__shared__", source);
        Assert.DoesNotContain("__shared__ float*", source);
        Assert.DoesNotContain("__shared__ int*", source);
    }
}
