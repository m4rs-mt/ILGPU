// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CpuBackendTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.BackendTests;

public sealed class CpuBackendTests : BackendTestBase
{
    public CpuBackendTests(ITestOutputHelper output) : base(output, BackendType.CPU) { }

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
}
