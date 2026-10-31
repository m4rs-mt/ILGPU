// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Runtime;
using ILGPU.Runtime.Metal;
using System.Collections.Generic;

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
        List<string> args =
        [
            $"MetalTargetOS.{targetOS}",
            $"MetalGPUFamily.{gpuFamily}",
        ];

        // In source fallback mode, pass KernelSource to the constructor.
        // Source compiled at runtime defaults to strict math; the fast math
        // modes opt into the Metal compiler's fast math.
        if (ctx.ActiveEmbedMode == KernelEmbedMode.Source)
        {
            args.Add("KernelSource");
            if (ctx.MathMode != MathMode.Default)
                args.Add("fastMath: true");
        }

        // Kernels whose thread storage may start at address 0 tell the
        // runtime, which warns on devices that place it there
        if (!ctx.NullSafeLocals)
            args.Add("nullSafeLocals: false");

        for (int i = 0; i < args.Count; i++)
            ctx.WriteLine(args[i] + (i + 1 < args.Count ? "," : ")"));
    }

    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine(
            $"    MetalAcceleratorCapabilities.FromGPUFamily(MetalGPUFamily.{gpuFamily});");
    }
}
