// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: QuaternionIRTests.cs
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

public sealed class QuaternionIRTests : CompilationTestBase
{
    public QuaternionIRTests(ITestOutputHelper output) : base(output) { }

    [Fact] public void QuaternionCtorFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionCtorFieldsKernel)));
    [Fact] public void QuaternionCtorVector3_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionCtorVector3Kernel)));
    [Fact] public void QuaternionIdentity_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionIdentityKernel)));
    [Fact] public void QuaternionAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionAddKernel)));
    [Fact] public void QuaternionSub_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionSubKernel)));
    [Fact] public void QuaternionMulQuat_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionMulQuatKernel)));
    [Fact] public void QuaternionMulScalar_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionMulScalarKernel)));
    [Fact] public void QuaternionDiv_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionDivKernel)));
    [Fact] public void QuaternionNegate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionNegateKernel)));
    [Fact] public void QuaternionDot_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionDotKernel)));
    [Fact] public void QuaternionLength_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionLengthKernel)));
    [Fact] public void QuaternionLengthSquared_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionLengthSquaredKernel)));
    [Fact] public void QuaternionNormalize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionNormalizeKernel)));
    [Fact] public void QuaternionInverse_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionInverseKernel)));
    [Fact] public void QuaternionConjugate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionConjugateKernel)));
    [Fact] public void QuaternionConcatenate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionConcatenateKernel)));
    [Fact] public void QuaternionCreateFromAxisAngle_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionCreateFromAxisAngleKernel)));
    [Fact] public void QuaternionCreateFromYawPitchRoll_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionCreateFromYawPitchRollKernel)));
    [Fact] public void QuaternionCreateFromRotationMatrix_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionCreateFromRotationMatrixKernel)));
    [Fact] public void QuaternionLerp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionLerpKernel)));
    [Fact] public void QuaternionSlerp_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.QuaternionSlerpKernel)));

    [Fact] public void Vector3TransformByQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Vector3TransformByQuaternionKernel)));
    [Fact] public void Vector2TransformByQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Vector2TransformByQuaternionKernel)));
    [Fact] public void Vector4TransformVector4ByQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Vector4TransformVector4ByQuaternionKernel)));
    [Fact] public void Matrix4x4CreateFromQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Matrix4x4CreateFromQuaternionKernel)));
    [Fact] public void Matrix4x4CreateFromAxisAngle_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Matrix4x4CreateFromAxisAngleKernel)));
    [Fact] public void Matrix4x4CreateFromYawPitchRoll_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Matrix4x4CreateFromYawPitchRollKernel)));
    [Fact] public void Matrix4x4TransformByQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(QuaternionKernels), nameof(QuaternionKernels.Matrix4x4TransformByQuaternionKernel)));
}
