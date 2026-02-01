// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelEntryPointIRTests.cs
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

public sealed class KernelEntryPointIRTests : CompilationTestBase
{
    public KernelEntryPointIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void Index1D_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index1DKernel)));

    [Fact]
    public void Index2D_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index2DKernel)));

    [Fact]
    public void Index3D_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index3DKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Index1D_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index1DKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Index2D_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index2DKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Index3D_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index3DKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Index1D_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(KernelEntryPointKernels), nameof(KernelEntryPointKernels.Index1DKernel)),
            backend, opt, mode);
}
