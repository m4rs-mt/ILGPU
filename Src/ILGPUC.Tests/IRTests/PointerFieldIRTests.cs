// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerFieldIRTests.cs
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

public sealed class PointerFieldIRTests(ITestOutputHelper output)
    : CompilationTestBase(output)
{
    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ConditionalField_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerFieldKernels), nameof(PointerFieldKernels.ConditionalFieldKernel)), opt, mode);

    // --- Address spaces ---

    // A holder whose pointer to a local is changed under a condition is selected
    // as a structure whose field points to local memory: the structure no longer
    // keeps the field generic, which Metal and OpenCL emitted as a pointer to
    // global memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    public void ConditionalField_FieldPointsToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpFieldKernel(
            nameof(PointerFieldKernels.ConditionalFieldKernel),
            backend,
            opt);
        Assert.Matches(@"%struct\.\d+ = type \{ ptr<i32> addrspace\(local\), i32 \}", ir);
        Assert.Matches(@"select i1 %\d+, %struct\.\d+ %\d+, %struct\.\d+ %\d+", ir);
        Assert.DoesNotMatch(@"type \{ ptr<i32>, i32 \}", ir);
    }

    private string DumpFieldKernel(
        string kernelName,
        BackendType backend,
        OptimizationLevel opt) =>
        DumpIRString(
            GetKernel(typeof(PointerFieldKernels), kernelName),
            IRDumpPoint.AfterBackendTransforms,
            new CompilationProperties(OptimizationLevel: opt),
            backend);
}
