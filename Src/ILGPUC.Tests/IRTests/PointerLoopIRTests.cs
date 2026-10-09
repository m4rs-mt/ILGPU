// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerLoopIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class PointerLoopIRTests(ITestOutputHelper output)
    : CompilationTestBase(output)
{
    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalLess_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerLoopKernels), nameof(PointerLoopKernels.LocalLessKernel)), opt, mode);

    // --- Address spaces ---

    // A pointer moved up to an end pointer it is compared with points to local
    // memory: the comparison no longer keeps the loop pointer generic, which
    // Metal and OpenCL emitted as a pointer to global memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void LocalLess_LoopPointerPointsToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(nameof(PointerLoopKernels.LocalLessKernel), backend, opt);
        Assert.Matches(@"phi ptr<i32> addrspace\(local\) \[", ir);
        Assert.Matches(@"icmp ult ptr<i32> addrspace\(local\) %\d+, %\d+", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    // Two pointers to shared memory compared with each other both point to
    // shared memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void SharedLess_LoopPointersPointToSharedMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(nameof(PointerLoopKernels.SharedLessKernel), backend, opt);
        Assert.Matches(@"icmp ult ptr<i32> addrspace\(shared\) %\d+, %\d+", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    private string DumpLoopKernel(
        string kernelName,
        BackendType backend,
        OptimizationLevel opt) =>
        DumpIRString(
            GetKernel(typeof(PointerLoopKernels), kernelName),
            IRDumpPoint.AfterBackendTransforms,
            new CompilationProperties(OptimizationLevel: opt),
            backend);
}
