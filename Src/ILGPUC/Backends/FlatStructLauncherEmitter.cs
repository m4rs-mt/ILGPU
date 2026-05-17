// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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
    public override bool PassesLaunchExtents => true;

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

        // Reshape the 1-D KernelConfig into explicit 3-D grid / block
        // dimensions. cuLaunchKernel / hipModuleLaunchKernel are intrinsically
        // 3-D; passing gridDimY = gridDimZ = blockDimY = blockDimZ = 1 would
        // force index.Y / index.Z to 0 inside an Index2D / Index3D kernel.
        EmitLaunchDimensions(ctx);
        ctx.WriteLine("");

        ctx.EmitArgsMarshalingBlock(parameters);

        // Hidden launch-extent arguments of implicitly grouped launches; the
        // kernel skips the threads that only fill up the last block.
        var extents = GetLaunchExtentExpressions(ctx);
        for (int i = 0; i < extents.Length; ++i)
        {
            ctx.WriteLine(
                $"args.{EntryPointLaunch.GetExtentParameterName(i)} = {extents[i]};");
        }
        if (extents.Length > 0)
            ctx.WriteLine("");

        ctx.WriteLine($"{ExceptionTypeName}.ThrowIfFailed(");
        ctx.IncrementIndent();
        ctx.WriteLine($"{ApiLaunchPrefix}.LaunchKernelWithStruct3D(");
        ctx.IncrementIndent();
        ctx.WriteLine("typedStream, typedKernel,");
        ctx.WriteLine("_ilgpuc_gX, _ilgpuc_gY, _ilgpuc_gZ,");
        ctx.WriteLine("_ilgpuc_bX, _ilgpuc_bY, _ilgpuc_bZ,");
        ctx.WriteLine("launchConfig.SharedMemoryBytes,");
        // Pass the codegen-computed CUDA parameter-buffer size, not
        // sizeof(KernelArgs). C# Sequential-layout rounds the struct size
        // up to its largest field's alignment, but the device kernel's
        // parameter list has no trailing pad -- mismatched sizes make
        // cuLaunchKernel return CUDA_ERROR_LAUNCH_OUT_OF_RESOURCES (701).
        ctx.WriteLine("ref args, KernelArgsSize));");
        ctx.DecrementIndent();
        ctx.DecrementIndent();
    }
}
