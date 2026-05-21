// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BoxingIRTests.cs
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

public sealed class BoxingIRTests : CompilationTestBase
{
    public BoxingIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void BoxUnboxStruct_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BoxingKernels), nameof(BoxingKernels.BoxUnboxStructKernel)));

    [Fact]
    public void BoxUnboxGeneric_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BoxingKernels), nameof(BoxingKernels.BoxUnboxGenericKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void BoxUnboxStruct_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BoxingKernels), nameof(BoxingKernels.BoxUnboxStructKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void BoxUnboxGeneric_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BoxingKernels), nameof(BoxingKernels.BoxUnboxGenericKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void BoxUnboxStruct_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BoxingKernels), nameof(BoxingKernels.BoxUnboxStructKernel)),
            backend, opt, mode);
}
