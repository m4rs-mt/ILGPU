// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FlatStructLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Shared launcher emitter base for CUDA and ROCm backends that use a flat
/// KernelArgs struct and LaunchKernelWithStruct dispatch.
/// </summary>
abstract class FlatStructLauncherEmitter : LauncherEmitter
{
    /// <summary>
    /// Returns the concrete kernel type name (e.g., "CudaKernel") for the
    /// <c>Unsafe.As</c> cast in the launch body.
    /// </summary>
    protected abstract string KernelTypeName { get; }

    /// <summary>
    /// Returns the concrete stream type name (e.g., "CudaStream") for the
    /// <c>Unsafe.As</c> cast in the launch body.
    /// </summary>
    protected abstract string StreamTypeName { get; }

    /// <summary>
    /// Returns the platform API type name (e.g., "CudaAPI") used to invoke
    /// <c>LaunchKernelWithStruct</c>.
    /// </summary>
    protected abstract string ApiTypeName { get; }

    /// <summary>
    /// Returns the platform exception type name (e.g., "CudaException") used
    /// for <c>ThrowIfFailed</c> error checking after the launch call.
    /// </summary>
    protected abstract string ExceptionTypeName { get; }

    /// <summary>
    /// Returns the prefix used to call <c>LaunchKernelWithStruct</c>.
    /// Defaults to <c>{ApiTypeName}.CurrentAPI</c> for instance-method APIs
    /// (e.g., CudaAPI). Override to return just <c>{ApiTypeName}</c> when
    /// the API methods are static.
    /// </summary>
    protected virtual string ApiLaunchPrefix => $"{ApiTypeName}.CurrentAPI";

    /// <inheritdoc/>
    public sealed override bool NeedsKernelArgsStruct => true;

    public sealed override void EmitLaunchBody(
        LauncherEmissionContext ctx,
        List<ParameterInfo> parameters)
    {
        ctx.WriteLine($"var typedKernel = ({KernelTypeName})_runtimeKernel!;");
        ctx.WriteLine($"var typedStream = ({StreamTypeName})stream;");
        ctx.WriteLine(
            "var launchConfig = stream.PrepareKernelLaunch(" +
            "typedKernel, config);");
        ctx.WriteLine(
            "launchConfig = typedKernel" +
            ".GetCombinedSharedMemoryConfig(launchConfig);");
        ctx.WriteLine("");

        ctx.EmitArgsMarshalingBlock(parameters);

        ctx.WriteLine($"{ExceptionTypeName}.ThrowIfFailed(");
        ctx.IncrementIndent();
        ctx.WriteLine($"{ApiLaunchPrefix}.LaunchKernelWithStruct(");
        ctx.IncrementIndent();
        ctx.WriteLine("typedStream, typedKernel, launchConfig,");
        ctx.WriteLine("ref args, sizeof(KernelArgs)));");
        ctx.DecrementIndent();
        ctx.DecrementIndent();
    }
}
