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

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void PointerToPointer_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerFieldKernels), nameof(PointerFieldKernels.PointerToPointerKernel)), opt, mode);

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

    // A holder assigned holders selected from its own value in a loop holds a
    // pointer to local memory, also in its second field: at O0, the stores of
    // values derived from its own loads kept it generic, and Metal and OpenCL
    // emitted its pointer as a pointer to global memory
    [Theory]
    [InlineData(nameof(PointerFieldKernels.SelfUpdatingHolderKernel), BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(nameof(PointerFieldKernels.SelfUpdatingHolderKernel), BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(nameof(PointerFieldKernels.SelfUpdatingHolderKernel), BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(nameof(PointerFieldKernels.SelfUpdatingCountedKernel), BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(nameof(PointerFieldKernels.SelfUpdatingCountedKernel), BackendType.OpenCL, OptimizationLevel.O2)]
    public void SelfUpdating_FieldPointsToLocalMemory(
        string kernelName,
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpFieldKernel(kernelName, backend, opt);
        Assert.Matches(
            @"%struct\.\d+ = type \{ (i32, )?ptr<i32> addrspace\(local\)(, i32)? \}",
            ir);
        Assert.Matches(@"phi %struct\.\d+ \[", ir);
        Assert.DoesNotMatch(@"type \{ (i32, )?ptr<i32>(, i32)? \}", ir);
    }

    // A structure holding a single pointer, assigned values selected from its
    // own value and incremented through an instance method in a loop, holds a
    // pointer to local memory: at O0, the phi of the selected values stayed
    // generic, and Metal and OpenCL emitted it as a pointer to global memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    public void SelfUpdatingCounter_PointerPointsToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpFieldKernel(
            nameof(PointerFieldKernels.SelfUpdatingCounterKernel),
            backend,
            opt);
        Assert.Matches(@"phi ptr<i32> addrspace\(local\) \[", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    // A pointer local whose address is taken and through which pointers to
    // locals are stored holds pointers to local memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void PointerToPointer_PointerLocalPointsToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpFieldKernel(
            nameof(PointerFieldKernels.PointerToPointerKernel),
            backend,
            opt);
        Assert.Contains("alloca ptr<i32> addrspace(local), i32 1", ir);
        Assert.DoesNotContain("alloca ptr<i32>, i32 1", ir);
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
