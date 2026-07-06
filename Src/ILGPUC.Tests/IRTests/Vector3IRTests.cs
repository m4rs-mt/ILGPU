// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector3IRTests.cs
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

public sealed class Vector3IRTests : CompilationTestBase
{
    public Vector3IRTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Vector3CtorFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3CtorFieldsKernel)));

    [Fact]
    public void Vector3Add_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3AddKernel)));

    [Fact]
    public void Vector3Sub_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3SubKernel)));

    [Fact]
    public void Vector3MulVec_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3MulVecKernel)));

    [Fact]
    public void Vector3MulScalar_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3MulScalarKernel)));

    [Fact]
    public void Vector3Div_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3DivKernel)));

    [Fact]
    public void Vector3Dot_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3DotKernel)));

    [Fact]
    public void Vector3Cross_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3CrossKernel)));

    [Fact]
    public void Vector3Length_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3LengthKernel)));

    [Fact]
    public void Vector3Normalize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3NormalizeKernel)));

    [Fact]
    public void Vector3Distance_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3DistanceKernel)));

    [Fact]
    public void Vector3Transform_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3TransformKernel)));

    [Fact]
    public void Vector3TransformNormal_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3TransformNormalKernel)));

    [Fact]
    public void Vector3Reflect_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3ReflectKernel)));

    [Fact]
    public void Vector3MinMax_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3MinMaxKernel)));

    [Fact]
    public void Vector3Clamp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3ClampKernel)));

    [Fact]
    public void Vector3Lerp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3LerpKernel)));

    [Fact]
    public void Vector3Abs_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3AbsKernel)));

    [Fact]
    public void Vector3TransformManual_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(Vector3Kernels), nameof(Vector3Kernels.Vector3TransformManualKernel)));
}
