// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix4x4IRTests.cs
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

public sealed class Matrix4x4IRTests : CompilationTestBase
{
    public Matrix4x4IRTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Matrix4x4Identity_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4IdentityKernel)));

    [Fact]
    public void Matrix4x4CreateTranslation_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateTranslationKernel)));

    [Fact]
    public void Matrix4x4CreateScale_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateScaleKernel)));

    [Fact]
    public void Matrix4x4CreateRotationX_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateRotationXKernel)));

    [Fact]
    public void Matrix4x4CreateRotationY_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateRotationYKernel)));

    [Fact]
    public void Matrix4x4CreateRotationZ_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateRotationZKernel)));

    [Fact]
    public void Matrix4x4CreatePerspectiveFov_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreatePerspectiveFovKernel)));

    [Fact]
    public void Matrix4x4CreateLookAt_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4CreateLookAtKernel)));

    [Fact]
    public void Matrix4x4Multiply_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4MultiplyKernel)));

    [Fact]
    public void Matrix4x4Transpose_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4TransposeKernel)));

    [Fact]
    public void Matrix4x4Determinant_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4DeterminantKernel)));

    [Fact]
    public void Matrix4x4Invert_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4InvertKernel)));

    [Fact]
    public void Matrix4x4Decompose_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4DecomposeKernel)));

    [Fact]
    public void Matrix4x4DecomposeIterative_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Matrix4x4Kernels), nameof(Matrix4x4Kernels.Matrix4x4DecomposeIterativeKernel)));
}
