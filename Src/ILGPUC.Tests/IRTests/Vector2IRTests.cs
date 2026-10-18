// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector2IRTests.cs
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

public sealed class Vector2IRTests : CompilationTestBase
{
    public Vector2IRTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Vector2CtorFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2CtorFieldsKernel)));

    [Fact]
    public void Vector2Add_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2AddKernel)));

    [Fact]
    public void Vector2Sub_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2SubKernel)));

    [Fact]
    public void Vector2MulVec_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2MulVecKernel)));

    [Fact]
    public void Vector2MulScalar_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2MulScalarKernel)));

    [Fact]
    public void Vector2Div_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2DivKernel)));

    [Fact]
    public void Vector2Dot_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2DotKernel)));

    [Fact]
    public void Vector2Length_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2LengthKernel)));

    [Fact]
    public void Vector2LengthSquared_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2LengthSquaredKernel)));

    [Fact]
    public void Vector2Normalize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2NormalizeKernel)));

    [Fact]
    public void Vector2Distance_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2DistanceKernel)));

    [Fact]
    public void Vector2Transform_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2TransformKernel)));

    [Fact]
    public void Vector2MinMax_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2MinMaxKernel)));

    [Fact]
    public void Vector2Clamp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2ClampKernel)));

    [Fact]
    public void Vector2Lerp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2LerpKernel)));

    [Fact]
    public void Vector2Abs_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector2Kernels), nameof(Vector2Kernels.Vector2AbsKernel)));
}
