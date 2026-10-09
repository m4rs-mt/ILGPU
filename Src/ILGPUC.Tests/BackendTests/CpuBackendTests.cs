// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CpuBackendTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
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
