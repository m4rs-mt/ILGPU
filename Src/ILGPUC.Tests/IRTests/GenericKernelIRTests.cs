// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericKernelIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class GenericKernelIRTests : CompilationTestBase
{
    public GenericKernelIRTests(ITestOutputHelper output) : base(output) { }

    private static readonly MethodInfo s_launchClosure =
        (typeof(GenericKernelKernels)
            .GetMethod(
                nameof(GenericKernelKernels.LaunchClosure),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new System.InvalidOperationException(
                "GenericKernelKernels.LaunchClosure not found"))
            .MakeGenericMethod(typeof(AddOffsetClosure), typeof(long));

    // --- AfterFrontend ---

    [Fact]
    public void LaunchClosure_AfterFrontend() =>
        VerifyAfterFrontend(s_launchClosure);

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LaunchClosure_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(s_launchClosure, opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LaunchClosure_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(s_launchClosure, backend, opt, mode);
}
