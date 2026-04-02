// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelEntryPointExecutionTests.cs
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

namespace ILGPUC.Tests.ExecutionTests;

public abstract class KernelEntryPointExecutionTests : ExecutionTestBase
{
    protected KernelEntryPointExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Index1D_ProducesCorrectOutput()
    {
        // Each thread stores its index: 0, 1, 2, 3
        await VerifyProgramOutputAsync(
            "TestPrograms/KernelEntryPoint/Index1D.cs",
            ["Kernels.Index1DKernel"],
            ["0", "1", "2", "3"]);
    }

    [SkippableFact]
    public async Task Index2D_ProducesCorrectOutput()
    {
        // Index2D(2,2), width=2: data[X+Y*2] = X+Y*1000
        // data[0]=0, data[1]=1, data[2]=1000, data[3]=1001
        await VerifyProgramOutputAsync(
            "TestPrograms/KernelEntryPoint/Index2D.cs",
            ["Kernels.Index2DKernel"],
            ["0", "1", "1000", "1001"]);
    }

    [SkippableFact]
    public async Task Index3D_ProducesCorrectOutput()
    {
        // Index3D(2,2,2), width=2, height=2: data[X+Y*2+Z*4] = X+Y*100+Z*10000
        await VerifyProgramOutputAsync(
            "TestPrograms/KernelEntryPoint/Index3D.cs",
            ["Kernels.Index3DKernel"],
            ["0", "1", "100", "101", "10000", "10001", "10100", "10101"]);
    }
}
