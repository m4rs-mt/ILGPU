// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterleaveFieldsIRTests.cs
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

public sealed class InterleaveFieldsIRTests : CompilationTestBase
{
    public InterleaveFieldsIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void InterleaveFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(InterleaveFieldsKernels),
            nameof(InterleaveFieldsKernels.InterleaveFieldsKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void InterleaveFields_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(InterleaveFieldsKernels),
            nameof(InterleaveFieldsKernels.InterleaveFieldsKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void InterleaveFields_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(InterleaveFieldsKernels),
            nameof(InterleaveFieldsKernels.InterleaveFieldsKernel)),
            backend, opt, mode);
}
