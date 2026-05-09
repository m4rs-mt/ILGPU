// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericKernelExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Family A.2 regression: generic-kernel struct-closure marshalling. The
/// kernel is generic over a closure type that captures field state; the
/// launcher must marshal the captured field through to the device. Pins
/// the FlatStructLauncherEmitter regression that landed on <c>temp5</c>.
/// </summary>
public abstract class GenericKernelExecutionTests : ExecutionTestBase
{
    protected GenericKernelExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GenericClosure_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/GenericClosure.cs",
            ["Kernels.LaunchClosureKernel"],
            ["20", "21", "22", "23"]);
    }
}
