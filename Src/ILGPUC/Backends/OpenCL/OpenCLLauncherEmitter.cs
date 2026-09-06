// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// OpenCL-specific launcher emitter using per-argument SetKernelArgumentUnsafe calls.
/// </summary>
sealed class OpenCLLauncherEmitter : PerArgumentLauncherEmitter
{
    public override string[] RequiredUsings => ["using ILGPU.Runtime.OpenCL;"];

    protected override void EmitPreamble(LauncherEmissionContext ctx)
    {
        ctx.WriteLine(
            "var clKernel = stream.GetCompiledKernel<CLKernel>(" +
            $"_runtimeKernel, {ctx.KernelNameLiteral}, " +
            $"{ctx.AcceleratorTypeExpression});");
        ctx.WriteLine("var clStream = (CLStream)stream;");
        ctx.WriteLine(
            "var launchConfig = stream.PrepareKernelLaunch(" +
            "clKernel, config);");
        ctx.WriteLine(
            "launchConfig = clKernel" +
            ".GetCombinedSharedMemoryConfig(launchConfig);");
        ctx.WriteLine("");
    }

    protected override void EmitPrimitiveArg(
        LauncherEmissionContext ctx,
        string expression,
        string marshaledTypeName,
        int argIndex)
    {
        ctx.WriteLine(
            $"CLException.ThrowIfFailed(CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, {expression}));");
    }

    protected override int EmitViewArg(
        LauncherEmissionContext ctx,
        string viewImplVar,
        string viewSourceExpr,
        int argIndex)
    {
        // cl_mem handle and byte offset (see
        // CLLanguageConfiguration.EmitNonFlattenedKernelParam), then length.
        var memory = $"_clMem_{argIndex}";
        var offset = $"_clOffset_{argIndex}";
        ctx.WriteLine(
            $"CLMemoryBuffer.ResolveView((IArrayView){viewSourceExpr}, " +
            $"(IntPtr){viewImplVar}.Ptr, out var {memory}, out var {offset});");
        argIndex = EmitBufferArgs(ctx, memory, offset, argIndex);
        ctx.WriteLine(
            $"CLException.ThrowIfFailed(CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, {viewImplVar}.Length));");
        argIndex++;
        return argIndex;
    }

    protected override int EmitPointerArg(
        LauncherEmissionContext ctx,
        string expression,
        int argIndex) =>
        EmitBufferArgs(ctx, $"(IntPtr){expression}", "0L", argIndex);

    /// <summary>
    /// Sets a buffer pointer argument and the byte-offset argument that
    /// follows it.
    /// </summary>
    /// <returns>The next argument index.</returns>
    private static int EmitBufferArgs(
        LauncherEmissionContext ctx,
        string memory,
        string offset,
        int argIndex)
    {
        ctx.WriteLine(
            $"CLException.ThrowIfFailed(CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, {memory}));");
        ctx.WriteLine(
            $"CLException.ThrowIfFailed(CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex + 1}, {offset}));");
        return argIndex + 2;
    }

    /// <inheritdoc/>
    public override bool PassesLaunchExtents => true;

    protected override void EmitPostamble(
        LauncherEmissionContext ctx,
        int nextArgIndex)
    {
        // Hidden launch-extent arguments of implicitly grouped launches
        // follow the kernel's own arguments; the kernel skips the work-items
        // that only fill up the last work-group.
        var extents = GetLaunchExtentExpressions(ctx);
        for (int i = 0; i < extents.Length; ++i)
        {
            ctx.WriteLine(
                $"CLException.ThrowIfFailed(CLAPI.CurrentAPI.SetKernelArgument(" +
                $"clKernel.KernelPtr, {nextArgIndex + i}, {extents[i]}));");
        }

        ctx.WriteLine("");
        EmitLaunchDimensions(ctx);
        ctx.WriteLine("");
        ctx.WriteLine(
            "CLException.ThrowIfFailed(" +
            "CLAPI.CurrentAPI.LaunchKernelWithStreamBinding3D(" +
            "clStream, clKernel, " +
            "_ilgpuc_gX, _ilgpuc_gY, _ilgpuc_gZ, " +
            "_ilgpuc_bX, _ilgpuc_bY, _ilgpuc_bZ));");
    }
}
