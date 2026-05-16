// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompareFloatIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class CompareFloatIRTests : CompilationTestBase
{
    public CompareFloatIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void LessThan_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(floatType));

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void LessEqual_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("LessEqualKernel")!.MakeGenericMethod(floatType));

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void GreaterThan_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("GreaterThanKernel")!.MakeGenericMethod(floatType));

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void GreaterEqual_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("GreaterEqualKernel")!.MakeGenericMethod(floatType));

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void Equal_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("EqualKernel")!.MakeGenericMethod(floatType));

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypes), MemberType = typeof(TestTypes))]
    public void NotEqual_AfterFrontend(Type floatType) =>
        VerifyAfterFrontend(
            typeof(CompareFloatKernels).GetMethod("NotEqualKernel")!.MakeGenericMethod(floatType));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessThan_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(floatType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessEqual_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("LessEqualKernel")!.MakeGenericMethod(floatType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void GreaterThan_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("GreaterThanKernel")!.MakeGenericMethod(floatType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void GreaterEqual_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("GreaterEqualKernel")!.MakeGenericMethod(floatType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Equal_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("EqualKernel")!.MakeGenericMethod(floatType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void NotEqual_AfterGlobalOpt(Type floatType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareFloatKernels).GetMethod("NotEqualKernel")!.MakeGenericMethod(floatType), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.FloatTypeAndBackendAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessThan_AfterBackendTransforms(Type floatType, BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(
            typeof(CompareFloatKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(floatType),
            backend, opt, mode);
}
