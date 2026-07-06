// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector4IRTests.cs
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

public sealed class Vector4IRTests : CompilationTestBase
{
    public Vector4IRTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Vector4CtorFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4CtorFieldsKernel)));

    [Fact]
    public void Vector4Add_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4AddKernel)));

    [Fact]
    public void Vector4Sub_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4SubKernel)));

    [Fact]
    public void Vector4MulVec_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4MulVecKernel)));

    [Fact]
    public void Vector4MulScalar_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4MulScalarKernel)));

    [Fact]
    public void Vector4Div_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4DivKernel)));

    [Fact]
    public void Vector4Dot_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4DotKernel)));

    [Fact]
    public void Vector4Length_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4LengthKernel)));

    [Fact]
    public void Vector4Normalize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4NormalizeKernel)));

    [Fact]
    public void Vector4Transform_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4TransformKernel)));

    [Fact]
    public void Vector4MinMax_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4MinMaxKernel)));

    [Fact]
    public void Vector4Clamp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4ClampKernel)));

    [Fact]
    public void Vector4Lerp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector4Kernels), nameof(Vector4Kernels.Vector4LerpKernel)));
}
