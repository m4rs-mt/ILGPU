// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Backends.Metal;

/// <summary>
/// Metal-specific launcher emitter using compute command encoder per-argument API.
/// </summary>
sealed class MetalLauncherEmitter : PerArgumentLauncherEmitter
{
    public override string[] RequiredUsings => ["using ILGPU.Runtime.Metal;"];

    protected override void EmitPreamble(LauncherEmissionContext ctx)
    {
        ctx.WriteLine("var metalKernel = (MetalKernel)_runtimeKernel!;");
        ctx.WriteLine("var metalStream = (MetalStream)stream;");
        ctx.WriteLine(
            "var launchConfig = stream.PrepareKernelLaunch(" +
            "metalKernel, config);");
        ctx.WriteLine(
            "launchConfig = metalKernel" +
            ".GetCombinedSharedMemoryConfig(launchConfig);");
        ctx.WriteLine("var commandBuffer = metalStream.CreateCommandBuffer();");
        ctx.WriteLine(
            "var encoder = MetalAPI.NewComputeCommandEncoder(commandBuffer);");
        ctx.WriteLine("");
        ctx.WriteLine(
            "MetalAPI.SetComputePipelineState(encoder, metalKernel.PipelineState);");
        ctx.WriteLine("");
    }

    protected override void EmitPrimitiveArg(
        LauncherEmissionContext ctx,
        string expression,
        string marshaledTypeName,
        int argIndex)
    {
        ctx.WriteLine(
            $"MetalAPI.SetBytes(encoder, {expression}, " +
            $"sizeof({marshaledTypeName}), {argIndex});");
    }

    protected override int EmitViewArg(
        LauncherEmissionContext ctx,
        string viewImplVar,
        string viewSourceExpr,
        int argIndex)
    {
        // Resolve the ArrayView's underlying MetalMemoryBuffer and byte offset
        // via IArrayView.Buffer, avoiding any global registry.
        ctx.WriteLine(
            $"MetalMemoryBuffer.ResolveView(" +
            $"(IArrayView){viewSourceExpr}, (IntPtr){viewImplVar}.Ptr, " +
            $"out var _mtlBuf_{argIndex}, out var _mtlOff_{argIndex});");
        ctx.WriteLine(
            $"MetalAPI.SetBuffer(encoder, " +
            $"_mtlBuf_{argIndex}, _mtlOff_{argIndex}, {argIndex});");
        argIndex++;
        ctx.WriteLine(
            $"MetalAPI.SetBytes(encoder, " +
            $"{viewImplVar}.Length, sizeof(long), {argIndex});");
        argIndex++;
        return argIndex;
    }

    protected override void EmitPointerArg(
        LauncherEmissionContext ctx,
        string expression,
        int argIndex)
    {
        // Raw pointer kernel arguments are not supported on Metal.
        // Metal requires MTLBuffer handles via [encoder setBuffer:offset:atIndex:],
        // which can only be obtained from an ArrayView's underlying buffer.
        ctx.WriteLine(
            "throw new System.NotSupportedException(" +
            "\"Raw pointer kernel arguments are not supported on Metal. " +
            "Use ArrayView<T> instead.\");");
    }

    protected override void EmitPostamble(
        LauncherEmissionContext ctx,
        int nextArgIndex)
    {
        ctx.WriteLine("");
        ctx.WriteLine("if (launchConfig.UsesSharedMemory)");
        ctx.OpenScope();
        ctx.WriteLine(
            "MetalAPI.SetThreadgroupMemoryLength(encoder, " +
            $"(ulong)launchConfig.SharedMemoryBytes, {nextArgIndex});");
        ctx.CloseScope();
        ctx.WriteLine("");
        // Use DispatchThreads to dispatch exactly the user's extent,
        // avoiding extra threads that would cause incorrect results
        // (e.g., atomics adding too many times).
        ctx.WriteLine("MetalAPI.DispatchThreads(encoder,");
        ctx.IncrementIndent();
        if (ctx.IndexDimensions >= 3)
        {
            ctx.WriteLine(
                "new Index3D(_dimX, _dimY, _dimZ), " +
                "new Index3D(launchConfig.GroupSize, 1, 1));");
        }
        else if (ctx.IndexDimensions == 2)
        {
            ctx.WriteLine(
                "new Index3D(_dimX, _dimY, 1), " +
                "new Index3D(launchConfig.GroupSize, 1, 1));");
        }
        else
        {
            ctx.WriteLine(
                "new Index3D((int)userExtent, 1, 1), " +
                "new Index3D(launchConfig.GroupSize, 1, 1));");
        }
        ctx.DecrementIndent();
        ctx.WriteLine("");
        ctx.WriteLine("MetalAPI.EndEncoding(encoder);");
        ctx.WriteLine("metalStream.CommitCommandBuffer(commandBuffer);");
    }
}
