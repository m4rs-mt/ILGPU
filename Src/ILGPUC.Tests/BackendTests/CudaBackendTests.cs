// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaBackendTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.BackendTests;

public sealed class CudaBackendTests : BackendTestBase
{
    public CudaBackendTests(ITestOutputHelper output) : base(output, BackendType.Cuda) { }

    [Theory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public void SourceGeneration(string kernelName) =>
        AssertSourceGenerationSucceeds(KernelRegistry.Resolve(kernelName));

    [Theory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public void SourceGeneration_Release(string kernelName) =>
        AssertSourceGenerationSucceeds(
            KernelRegistry.Resolve(kernelName),
            new CompilationProperties().WithMode(CompilationMode.Release));

    // Native compilation — runs all kernels through nvcc.
    // Skips if nvcc is unavailable or the kernel requires unsupported capabilities.
    [SkippableTheory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public async Task NativeCompilation(string kernelName) =>
        await AssertNativeCompilationSucceeds(
            KernelRegistry.Resolve(kernelName),
            required: KernelRegistry.GetRequiredCapabilities(kernelName));
}
