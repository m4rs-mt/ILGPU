// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaCompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ILGPUC.Backends.Cuda;

/// <summary>
/// CUDA-specific compiled kernel emitter that generates classes deriving from
/// <see cref="CudaCompiledKernel"/>.
/// </summary>
sealed class CudaCompiledKernelEmitter(
    AcceleratorArchitecture architecture,
    CudaInstructionSet instructionSet) : CompiledKernelEmitter
{
    /// <inheritdoc/>
    public override string BaseClassName => "CudaCompiledKernel";

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.Cuda;

    /// <inheritdoc/>
    public override KernelEmbedMode EmbedMode => KernelEmbedMode.Binary;

    /// <inheritdoc/>
    public override string[] RequiredUsings =>
    [
        "using ILGPU.Runtime.Cuda;"
    ];

    /// <inheritdoc/>
    public override void EmitPlatformConstructorArgs(LauncherEmissionContext ctx)
    {
        ctx.WriteLine(
            $"new AcceleratorArchitecture(" +
            $"{architecture.Major}, {architecture.Minor}),");
        ctx.WriteLine(
            $"new CudaInstructionSet(" +
            $"{instructionSet.Major}, {instructionSet.Minor}))");
    }

    /// <inheritdoc/>
    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine(
            $"    CudaAcceleratorCapabilities.FromArchitecture(" +
            $"new AcceleratorArchitecture({architecture.Major}, {architecture.Minor}));");
    }
}
