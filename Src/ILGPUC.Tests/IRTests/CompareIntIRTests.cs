// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompareIntIRTests.cs
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

public sealed class CompareIntIRTests : CompilationTestBase
{
    public CompareIntIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void LessThan_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void LessEqual_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("LessEqualKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void GreaterThan_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("GreaterThanKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void GreaterEqual_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("GreaterEqualKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Equal_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("EqualKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void NotEqual_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(CompareIntKernels).GetMethod("NotEqualKernel")!.MakeGenericMethod(intType));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessThan_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessEqual_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("LessEqualKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void GreaterThan_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("GreaterThanKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void GreaterEqual_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("GreaterEqualKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Equal_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("EqualKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void NotEqual_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(CompareIntKernels).GetMethod("NotEqualKernel")!.MakeGenericMethod(intType), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndBackendAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void LessThan_AfterBackendTransforms(Type intType, BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(
            typeof(CompareIntKernels).GetMethod("LessThanKernel")!.MakeGenericMethod(intType),
            backend, opt, mode);
}
