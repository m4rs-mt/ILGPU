// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPUC.Backends.ROCm;

/// <summary>
/// ROCm-specific compiled kernel emitter that generates classes deriving from
/// ROCmCompiledKernel.
/// </summary>
sealed class ROCmCompiledKernelEmitter(string targetArchitecture) : CompiledKernelEmitter
{
    public override string BaseClassName => "ROCmCompiledKernel";

    public override AcceleratorType AcceleratorType => AcceleratorType.ROCm;

    public override KernelEmbedMode EmbedMode => KernelEmbedMode.Binary;

    public override string[] RequiredUsings =>
    [
        "using ILGPU.Runtime.ROCm;"
    ];

    public override void EmitPlatformConstructorArgs(LauncherEmissionContext ctx)
    {
        ctx.WriteLine($"\"{targetArchitecture}\")");
    }

    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine(
            $"    ROCmAcceleratorCapabilities.FromArchitectureString(\"{targetArchitecture}\");");
    }
}
