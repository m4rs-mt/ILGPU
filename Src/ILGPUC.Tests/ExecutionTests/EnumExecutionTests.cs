// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: EnumExecutionTests.cs
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

public abstract class EnumExecutionTests : ExecutionTestBase
{
    protected EnumExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task IntEnum_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Enum/IntEnum.cs",
            ["Kernels.IntEnumKernel"],
            ["200", "200", "200", "200"]);
    }

    [SkippableFact]
    public async Task ByteEnum_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Enum/ByteEnum.cs",
            ["Kernels.ByteEnumKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task ShortEnum_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Enum/ShortEnum.cs",
            ["Kernels.ShortEnumKernel"],
            ["20", "20", "20", "20"]);
    }

    [SkippableFact]
    public async Task LongEnum_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Enum/LongEnum.cs",
            ["Kernels.LongEnumKernel"],
            ["2000", "2000", "2000", "2000"]);
    }
}
