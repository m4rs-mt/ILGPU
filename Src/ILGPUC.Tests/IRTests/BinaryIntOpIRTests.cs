// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BinaryIntOpIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class BinaryIntOpIRTests : CompilationTestBase
{
    public BinaryIntOpIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Add_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("AddKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Sub_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("SubKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Mul_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("MulKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Div_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("DivKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void And_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("AndKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Or_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("OrKernel")!.MakeGenericMethod(intType));

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypes), MemberType = typeof(TestTypes))]
    public void Xor_AfterFrontend(Type intType) =>
        VerifyAfterFrontend(
            typeof(BinaryIntOpKernels).GetMethod("XorKernel")!.MakeGenericMethod(intType));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Add_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("AddKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Sub_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("SubKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Mul_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("MulKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Div_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("DivKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void And_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("AndKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Or_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("OrKernel")!.MakeGenericMethod(intType), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Xor_AfterGlobalOpt(Type intType, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(
            typeof(BinaryIntOpKernels).GetMethod("XorKernel")!.MakeGenericMethod(intType), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.IntegerTypeAndBackendAndOptLevelAndMode), MemberType = typeof(TestTypes))]
    public void Add_AfterBackendTransforms(Type intType, BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(
            typeof(BinaryIntOpKernels).GetMethod("AddKernel")!.MakeGenericMethod(intType),
            backend, opt, mode);
}
