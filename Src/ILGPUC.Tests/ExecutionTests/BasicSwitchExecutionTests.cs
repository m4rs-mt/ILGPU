// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicSwitchExecutionTests.cs
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

public abstract class BasicSwitchExecutionTests : ExecutionTestBase
{
    protected BasicSwitchExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SwitchMap_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicSwitch/SwitchMap.cs",
            ["Kernels.SwitchMapKernel"],
            ["30", "30", "30", "30"]);
    }

    [SkippableFact]
    public async Task SwitchStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicSwitch/SwitchStore.cs",
            ["Kernels.SwitchStoreKernel"],
            ["100", "101", "102", "103"]);
    }
}
