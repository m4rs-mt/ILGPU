// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PlaneIRTests.cs
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

public sealed class PlaneIRTests : CompilationTestBase
{
    public PlaneIRTests(ITestOutputHelper output) : base(output) { }

    [Fact] public void PlaneCtorFields_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneCtorFieldsKernel)));
    [Fact] public void PlaneCtorVector3_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneCtorVector3Kernel)));
    [Fact] public void PlaneCtorVector4_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneCtorVector4Kernel)));
    [Fact] public void PlaneCreateFromVertices_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneCreateFromVerticesKernel)));
    [Fact] public void PlaneDot_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneDotKernel)));
    [Fact] public void PlaneDotCoordinate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneDotCoordinateKernel)));
    [Fact] public void PlaneDotNormal_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneDotNormalKernel)));
    [Fact] public void PlaneNormalize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneNormalizeKernel)));
    [Fact] public void PlaneTransformByMatrix_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneTransformByMatrixKernel)));
    [Fact] public void PlaneTransformByQuaternion_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(typeof(PlaneKernels), nameof(PlaneKernels.PlaneTransformByQuaternionKernel)));
}
