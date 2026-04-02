// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: EnumIRTests.cs
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

public sealed class EnumIRTests : CompilationTestBase
{
    public EnumIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void ByteEnum_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.ByteEnumKernel)));

    [Fact]
    public void ShortEnum_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.ShortEnumKernel)));

    [Fact]
    public void IntEnum_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.IntEnumKernel)));

    [Fact]
    public void LongEnum_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.LongEnumKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ByteEnum_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.ByteEnumKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ShortEnum_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.ShortEnumKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IntEnum_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.IntEnumKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LongEnum_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.LongEnumKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ByteEnum_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(EnumKernels), nameof(EnumKernels.ByteEnumKernel)),
            backend, opt, mode);
}
