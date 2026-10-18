// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix3x2IRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class Matrix3x2IRTests : CompilationTestBase
{
    public Matrix3x2IRTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Matrix3x2Identity_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2IdentityKernel)));

    [Fact]
    public void Matrix3x2CreateTranslation_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2CreateTranslationKernel)));

    [Fact]
    public void Matrix3x2CreateRotation_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2CreateRotationKernel)));

    [Fact]
    public void Matrix3x2CreateScale_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2CreateScaleKernel)));

    [Fact]
    public void Matrix3x2Multiply_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2MultiplyKernel)));

    [Fact]
    public void Matrix3x2Invert_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix3x2Kernels), nameof(Matrix3x2Kernels.Matrix3x2InvertKernel)));
}
