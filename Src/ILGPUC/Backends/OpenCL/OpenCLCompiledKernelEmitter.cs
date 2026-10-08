// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// OpenCL-specific compiled kernel emitter that generates classes deriving from
/// CLCompiledKernel.
/// </summary>
sealed class OpenCLCompiledKernelEmitter(
    int clcMajor,
    int clcMinor,
    KernelEmbedMode embedMode = KernelEmbedMode.Source) : CompiledKernelEmitter
{
    public override string BaseClassName => "CLCompiledKernel";

    public override AcceleratorType AcceleratorType => AcceleratorType.OpenCL;

    public override KernelEmbedMode EmbedMode => embedMode;

    public override string[] RequiredUsings =>
    [
        "using ILGPU.Runtime.OpenCL;"
    ];

    public override void EmitPlatformConstructorArgs(LauncherEmissionContext ctx)
    {
        // In source fallback mode (binary requested but unavailable),
        // pass KernelSource to the CLCompiledKernel base constructor
        if (ctx.ActiveEmbedMode == KernelEmbedMode.Source
            && embedMode == KernelEmbedMode.Binary)
        {
            ctx.WriteLine($"new CLCVersion({clcMajor}, {clcMinor}),");
            ctx.WriteLine("KernelSource)");
        }
        else
        {
            ctx.WriteLine($"new CLCVersion({clcMajor}, {clcMinor}))");
        }
    }

    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine("    new CLAcceleratorCapabilities();");
    }
}
