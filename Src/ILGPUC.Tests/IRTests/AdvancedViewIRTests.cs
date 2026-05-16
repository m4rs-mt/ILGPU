// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AdvancedViewIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class AdvancedViewIRTests : CompilationTestBase
{
    public AdvancedViewIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void ViewOfStructCounter_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(AdvancedViewKernels),
            nameof(AdvancedViewKernels.ViewOfStructCounterKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ViewOfStructCounter_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(AdvancedViewKernels),
            nameof(AdvancedViewKernels.ViewOfStructCounterKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ViewOfStructCounter_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(AdvancedViewKernels),
            nameof(AdvancedViewKernels.ViewOfStructCounterKernel)),
            backend, opt, mode);
}
