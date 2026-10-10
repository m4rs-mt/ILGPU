// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RadixSortIRTests.cs
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

public sealed class RadixSortIRTests : CompilationTestBase
{
    public RadixSortIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void WarpRadixSortAscendingInt32_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(RadixSortKernels),
            nameof(RadixSortKernels.WarpRadixSortAscendingInt32Kernel)));

    [Fact]
    public void GroupRadixSortAscendingInt32_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(RadixSortKernels),
            nameof(RadixSortKernels.GroupRadixSortAscendingInt32Kernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpRadixSortAscendingInt32_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(RadixSortKernels),
            nameof(RadixSortKernels.WarpRadixSortAscendingInt32Kernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupRadixSortAscendingInt32_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(RadixSortKernels),
            nameof(RadixSortKernels.GroupRadixSortAscendingInt32Kernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpRadixSortAscendingInt32_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(RadixSortKernels),
            nameof(RadixSortKernels.WarpRadixSortAscendingInt32Kernel)),
            backend, opt, mode);
}
