// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Backends.ROCm;

/// <summary>
/// ROCm/HIP-specific launcher emitter using ROCmAPI.LaunchKernelWithStruct.
/// </summary>
sealed class ROCmLauncherEmitter : FlatStructLauncherEmitter
{
    public override string[] RequiredUsings => ["using ILGPU.Runtime.ROCm;"];

    protected override string KernelTypeName => "ROCmKernel";
    protected override string StreamTypeName => "ROCmStream";
    protected override string ApiTypeName => "ROCmAPI";
    protected override string ExceptionTypeName => "ROCmException";

    // ROCmAPI.LaunchKernelWithStruct is an instance method on the singleton
    // ROCmAPI.CurrentAPI (same shape as CudaAPI). Inherit the base prefix
    // (which produces "ROCmAPI.CurrentAPI") instead of overriding.
}
