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

        // Reshape the 1-D KernelConfig (returned by ComputeKernelConfig) into
        // explicit 3-D grid / block dimensions that match the user extent.
        // CUDA's cuLaunchKernel is intrinsically 3-D; passing
        // gridDimY = gridDimZ = blockDimY = blockDimZ = 1 silently forces
        // index.Y / index.Z to 0 inside the kernel even though the IL kernel
        // was generated for an Index2D / Index3D entry. For 1-D launches with
        // userExtent <= GroupSize we shrink the block so we do not run more
        // threads than the user asked for -- this removes the per-kernel
        // bounds check the runtime API would otherwise install via the
        // lambda wrapper (which the Roslyn rewriter strips at the launch
        // site).
        EmitLaunchDimensions(ctx);
        ctx.WriteLine("");

        ctx.EmitArgsMarshalingBlock(parameters);

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

    /// <summary>
    /// Emits a small block of C# that derives explicit 3-D grid and block
    /// dimensions (<c>_ilgpuc_gX/Y/Z</c> / <c>_ilgpuc_bX/Y/Z</c>) from the
    /// <c>userExtent</c>, the multi-dim <c>_dimX</c>/<c>_dimY</c>/<c>_dimZ</c>
    /// (when present) and the auto-grouping <c>launchConfig.GroupSize</c>.
    /// </summary>
    private static void EmitLaunchDimensions(LauncherEmissionContext ctx)
    {
        ctx.WriteLine(
            "int _ilgpuc_groupSize = launchConfig.GroupSize > 0 " +
            "? launchConfig.GroupSize : 1;");
        ctx.WriteLine("int _ilgpuc_bX, _ilgpuc_bY, _ilgpuc_bZ;");
        ctx.WriteLine("int _ilgpuc_gX, _ilgpuc_gY, _ilgpuc_gZ;");
        switch (ctx.IndexDimensions)
        {
            case 3:
                // Distribute the auto group budget across the three axes,
                // capping each per-axis block at the user extent so we
                // never launch more threads than the user requested for
                // the fast path where the whole extent fits in one block.
                ctx.WriteLine(
                    "_ilgpuc_bX = " +
                    "System.Math.Min(_dimX, _ilgpuc_groupSize);");
                ctx.WriteLine("if (_ilgpuc_bX < 1) _ilgpuc_bX = 1;");
                ctx.WriteLine(
                    "_ilgpuc_bY = System.Math.Min(_dimY, " +
                    "System.Math.Max(1, _ilgpuc_groupSize / _ilgpuc_bX));");
                ctx.WriteLine(
                    "_ilgpuc_bZ = System.Math.Min(_dimZ, " +
                    "System.Math.Max(1, _ilgpuc_groupSize / " +
                    "(_ilgpuc_bX * _ilgpuc_bY)));");
                ctx.WriteLine(
                    "_ilgpuc_gX = (_dimX + _ilgpuc_bX - 1) / _ilgpuc_bX;");
                ctx.WriteLine(
                    "_ilgpuc_gY = (_dimY + _ilgpuc_bY - 1) / _ilgpuc_bY;");
                ctx.WriteLine(
                    "_ilgpuc_gZ = (_dimZ + _ilgpuc_bZ - 1) / _ilgpuc_bZ;");
                break;
            case 2:
                ctx.WriteLine(
                    "_ilgpuc_bX = " +
                    "System.Math.Min(_dimX, _ilgpuc_groupSize);");
                ctx.WriteLine("if (_ilgpuc_bX < 1) _ilgpuc_bX = 1;");
                ctx.WriteLine(
                    "_ilgpuc_bY = System.Math.Min(_dimY, " +
                    "System.Math.Max(1, _ilgpuc_groupSize / _ilgpuc_bX));");
                ctx.WriteLine("_ilgpuc_bZ = 1;");
                ctx.WriteLine(
                    "_ilgpuc_gX = (_dimX + _ilgpuc_bX - 1) / _ilgpuc_bX;");
                ctx.WriteLine(
                    "_ilgpuc_gY = (_dimY + _ilgpuc_bY - 1) / _ilgpuc_bY;");
                ctx.WriteLine("_ilgpuc_gZ = 1;");
                break;
            default:
                // 1-D auto-grouped path: shrink the block to userExtent when
                // it fits so we do not run for OOB thread ids. Grouped
                // launches (KernelIndex entry point) take this path too but
                // their userExtent already equals GridSize * GroupSize so
                // the math collapses to gridX = GridSize, blockX = GroupSize.
                if (ctx.KernelIndexTypeName is not null)
                {
                    ctx.WriteLine(
                        "_ilgpuc_bX = _ilgpuc_groupSize;");
                    ctx.WriteLine(
                        "_ilgpuc_gX = (int)launchConfig.GridSize;");
                }
                else
                {
                    ctx.WriteLine(
                        "long _ilgpuc_extent = userExtent > 0 ? userExtent " +
                        ": (long)launchConfig.GridSize * _ilgpuc_groupSize;");
                    // If the whole extent fits in a single device block,
                    // launch exactly userExtent threads -- this removes
                    // OOB-thread side-effects (extra atomics, OOB stores)
                    // that the runtime API would otherwise filter via the
                    // lambda-wrapped bounds check that the Roslyn
                    // rewriter strips at the launch site.
                    ctx.WriteLine(
                        "int _ilgpuc_maxBlock = " +
                        "stream.Accelerator?.MaxNumThreadsPerGroup ?? " +
                        "_ilgpuc_groupSize;");
                    ctx.WriteLine(
                        "_ilgpuc_bX = _ilgpuc_extent <= _ilgpuc_maxBlock " +
                        "? (int)_ilgpuc_extent " +
                        ": _ilgpuc_groupSize;");
                    ctx.WriteLine("if (_ilgpuc_bX < 1) _ilgpuc_bX = 1;");
                    ctx.WriteLine(
                        "_ilgpuc_gX = (int)" +
                        "((_ilgpuc_extent + _ilgpuc_bX - 1) / _ilgpuc_bX);");
                    ctx.WriteLine("if (_ilgpuc_gX < 1) _ilgpuc_gX = 1;");
                }
                ctx.WriteLine("_ilgpuc_bY = 1;");
                ctx.WriteLine("_ilgpuc_bZ = 1;");
                ctx.WriteLine("_ilgpuc_gY = 1;");
                ctx.WriteLine("_ilgpuc_gZ = 1;");
                break;
        }
    }
}
