// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupScanRuntimeExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Group scan execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the scan into a compile-time constant.
/// </summary>
public abstract class GroupScanRuntimeExecutionTests : ExecutionTestBase
{
    protected GroupScanRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupInclusiveScanAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupScanRuntime/GroupInclusiveScanAddRuntime.cs",
            ["Kernels.GroupInclusiveScanAddRuntimeKernel"],
            ["1", "3", "6", "10"]);
    }

    [SkippableFact]
    public async Task GroupExclusiveScanAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupScanRuntime/GroupExclusiveScanAddRuntime.cs",
            ["Kernels.GroupExclusiveScanAddRuntimeKernel"],
            ["0", "1", "3", "6"]);
    }
}
