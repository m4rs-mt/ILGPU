// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayViewIRTests.cs
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

public sealed class ArrayViewIRTests : CompilationTestBase
{
    public ArrayViewIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void Length_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.LengthKernel)));

    [Fact]
    public void IsValid_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.IsValidKernel)));

    [Fact]
    public void LoadStore_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.LoadStoreKernel)));

    [Fact]
    public void SubView_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.SubViewKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Length_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.LengthKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IsValid_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.IsValidKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LoadStore_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.LoadStoreKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SubView_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.SubViewKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Length_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ArrayViewKernels), nameof(ArrayViewKernels.LengthKernel)),
            backend, opt, mode);
}
