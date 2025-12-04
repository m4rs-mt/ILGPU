// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Metal;

namespace ILGPUC.Backends.Metal;

/// <summary>
/// Metal-specific compiled kernel emitter that generates classes deriving from
/// MetalCompiledKernel.
/// </summary>
sealed class MetalCompiledKernelEmitter(
    MetalTargetOS targetOS,
    MetalGPUFamily gpuFamily) : CompiledKernelEmitter
{
    public override string BaseClassName => "MetalCompiledKernel";

    public override AcceleratorType AcceleratorType => AcceleratorType.Metal;

    public override KernelEmbedMode EmbedMode => KernelEmbedMode.Binary;

    public override string[] RequiredUsings =>
    [
        "using ILGPU.Runtime.Metal;"
    ];

    public override void EmitPlatformConstructorArgs(LauncherEmissionContext ctx)
    {
        ctx.WriteLine($"MetalTargetOS.{targetOS},");
        // In source fallback mode, pass KernelSource to the constructor
        if (ctx.ActiveEmbedMode == KernelEmbedMode.Source)
            ctx.WriteLine($"MetalGPUFamily.{gpuFamily},");
        else
            ctx.WriteLine($"MetalGPUFamily.{gpuFamily})");

        if (ctx.ActiveEmbedMode == KernelEmbedMode.Source)
            ctx.WriteLine("KernelSource)");
    }

    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine(
            $"    MetalAcceleratorCapabilities.FromGPUFamily(MetalGPUFamily.{gpuFamily});");
    }
}
