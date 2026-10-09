// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLBackendTests.cs
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

public sealed class OpenCLBackendTests : BackendTestBase
{
    public OpenCLBackendTests(ITestOutputHelper output)
        : base(output, BackendType.OpenCL) { }

    [SkippableTheory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public void SourceGeneration(string kernelName) =>
        AssertSourceGenerationSucceeds(
            KernelRegistry.Resolve(kernelName),
            required: KernelRegistry.GetRequiredCapabilities(kernelName));

    [SkippableTheory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public void SourceGeneration_Release(string kernelName) =>
        AssertSourceGenerationSucceeds(
            KernelRegistry.Resolve(kernelName),
            new CompilationProperties().WithMode(CompilationMode.Release),
            required: KernelRegistry.GetRequiredCapabilities(kernelName));

    // Native compilation — runs all kernels through ocloc.
    // Skips if ocloc is unavailable or the kernel requires unsupported capabilities.
    [SkippableTheory]
    [MemberData(nameof(KernelRegistry.AllKernelNames), MemberType = typeof(KernelRegistry))]
    public async Task NativeCompilation(string kernelName) =>
        await AssertNativeCompilationSucceeds(
            KernelRegistry.Resolve(kernelName),
            required: KernelRegistry.GetRequiredCapabilities(kernelName),
            knownFailing: KernelRegistry.GetKnownFailing(kernelName));
}
