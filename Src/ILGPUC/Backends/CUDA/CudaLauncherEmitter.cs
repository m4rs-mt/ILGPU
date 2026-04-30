// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Backends.Cuda;

/// <summary>
/// CUDA-specific launcher emitter using CudaAPI.LaunchKernelWithStruct.
/// </summary>
sealed class CudaLauncherEmitter : FlatStructLauncherEmitter
{
    public override string[] RequiredUsings => ["using ILGPU.Runtime.Cuda;"];

    protected override string KernelTypeName => "CudaKernel";
    protected override string StreamTypeName => "CudaStream";
    protected override string ApiTypeName => "CudaAPI";
    protected override string ExceptionTypeName => "CudaException";
}
