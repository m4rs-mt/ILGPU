// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueTupleIRTests.cs
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

public sealed class ValueTupleIRTests : CompilationTestBase
{
    public ValueTupleIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void TupleCreate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ValueTupleKernels), nameof(ValueTupleKernels.TupleCreateKernel)));

    [Fact]
    public void TuplePass_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ValueTupleKernels), nameof(ValueTupleKernels.TuplePassKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void TupleCreate_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ValueTupleKernels), nameof(ValueTupleKernels.TupleCreateKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void TuplePass_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ValueTupleKernels), nameof(ValueTupleKernels.TuplePassKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void TupleCreate_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ValueTupleKernels), nameof(ValueTupleKernels.TupleCreateKernel)),
            backend, opt, mode);
}
