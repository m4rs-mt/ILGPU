// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// CPU-specific compiled kernel emitter that generates classes deriving from
/// CSharpCompiledKernel.
/// </summary>
sealed class CPUCompiledKernelEmitter(
    int simdWidth,
    string kernelClassName) : CompiledKernelEmitter
{
    public override string BaseClassName => "CPUCompiledKernel";

    public override AcceleratorType AcceleratorType => AcceleratorType.CPU;

    public override KernelEmbedMode EmbedMode => KernelEmbedMode.InlineCode;

    public override string[] RequiredUsings =>
    [
        "using ILGPU.Runtime.CPU;"
    ];

    public override void EmitPlatformConstructorArgs(LauncherEmissionContext ctx)
    {
        ctx.WriteLine($"{simdWidth},");
        ctx.WriteLine($"\"{kernelClassName}\")");
    }

    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine("    CPUAcceleratorCapabilities.Default;");
    }
}
