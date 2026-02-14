// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicSwitchIRTests.cs
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

public sealed class BasicSwitchIRTests : CompilationTestBase
{
    public BasicSwitchIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void SwitchMap_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicSwitchKernels), nameof(BasicSwitchKernels.SwitchMapKernel)));

    [Fact]
    public void SwitchStore_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicSwitchKernels), nameof(BasicSwitchKernels.SwitchStoreKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SwitchMap_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicSwitchKernels), nameof(BasicSwitchKernels.SwitchMapKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SwitchStore_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicSwitchKernels), nameof(BasicSwitchKernels.SwitchStoreKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SwitchMap_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicSwitchKernels), nameof(BasicSwitchKernels.SwitchMapKernel)),
            backend, opt, mode);
}
