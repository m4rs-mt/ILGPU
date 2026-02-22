// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GridIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class GridIRTests : CompilationTestBase
{
    public GridIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void GridDimension_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GridKernels), nameof(GridKernels.GridDimensionKernel)));

    [Fact]
    public void GridIndex_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GridKernels), nameof(GridKernels.GridIndexKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GridDimension_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GridKernels), nameof(GridKernels.GridDimensionKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GridIndex_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GridKernels), nameof(GridKernels.GridIndexKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GridDimension_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GridKernels), nameof(GridKernels.GridDimensionKernel)),
            backend, opt, mode);
}
