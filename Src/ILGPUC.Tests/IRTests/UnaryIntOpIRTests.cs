// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnaryIntOpIRTests.cs
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

public sealed class UnaryIntOpIRTests : CompilationTestBase
{
    public UnaryIntOpIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Neg_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(UnaryIntOpKernels).GetMethod("NegKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Not_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(UnaryIntOpKernels).GetMethod("NotKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Abs_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(UnaryIntOpKernels).GetMethod("AbsKernel")!.MakeGenericMethod(intType));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Neg_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(UnaryIntOpKernels).GetMethod("NegKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Not_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(UnaryIntOpKernels).GetMethod("NotKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Abs_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(UnaryIntOpKernels).GetMethod("AbsKernel")!.MakeGenericMethod(intType), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndBackendAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Neg_AfterBackendTransforms(Type intType, BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(
            typeof(UnaryIntOpKernels).GetMethod("NegKernel")!.MakeGenericMethod(intType),
            backend, opt, mode);
}
