// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MatrixMultiplyIRTests.cs
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

public sealed class MatrixMultiplyIRTests : CompilationTestBase
{
    public MatrixMultiplyIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void MatrixMultiplyTiled_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(MatrixMultiplyKernels),
            nameof(MatrixMultiplyKernels.MatrixMultiplyTiledKernel)));

    // --- AfterGlobalOpt ---
    // O0 is excluded: KernelIndex + GetSharedMemory2D triggers a pre-existing
    // StructureValue type-mismatch during address-space rewriting at O0.

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsO1PlusAndModes), MemberType = typeof(TestTypes))]
    public void MatrixMultiplyTiled_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(MatrixMultiplyKernels),
            nameof(MatrixMultiplyKernels.MatrixMultiplyTiledKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsO1PlusAndModes), MemberType = typeof(TestTypes))]
    public void MatrixMultiplyTiled_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(MatrixMultiplyKernels),
            nameof(MatrixMultiplyKernels.MatrixMultiplyTiledKernel)),
            backend, opt, mode);
}
