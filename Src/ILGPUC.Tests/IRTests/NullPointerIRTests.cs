// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullPointerIRTests.cs
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

public sealed class NullPointerIRTests(ITestOutputHelper output)
    : CompilationTestBase(output)
{
    // --- AfterFrontend ---

    [Fact]
    public void LocalNullCheck_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.LocalNullCheckKernel)));

    [Fact]
    public void HelperNullCheck_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.HelperNullCheckKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalNullCheck_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.LocalNullCheckKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void HelperNullCheck_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.HelperNullCheckKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalNullCheck_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.LocalNullCheckKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void HelperNullCheck_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(NullPointerKernels), nameof(NullPointerKernels.HelperNullCheckKernel)),
            backend, opt, mode);

    // --- Address spaces ---

    // Unsafe.NullRef passed to a helper that tests its reference parameter for
    // null, which is also called with a reference to a local, is the null
    // pointer of local memory. O0 keeps the call of Unsafe.NullRef, whose BCL
    // body would return a generic null reference
    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void RefHelperNullCheck_NullRefPointsToLocalMemory(BackendType backend)
    {
        var ir = DumpIRString(
            GetKernel(
                typeof(NullPointerKernels),
                nameof(NullPointerKernels.RefHelperNullCheckKernel)),
            IRDumpPoint.AfterBackendTransforms,
            new CompilationProperties(OptimizationLevel: OptimizationLevel.O0),
            backend);
        Assert.Contains("define i32 @ReadRefOrDefault(ptr<i32> addrspace(local) %p0", ir);
        Assert.Contains("@ReadRefOrDefault(ptr<i32> addrspace(local) null", ir);
    }
}
