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
        ctx.WriteLine("var clKernel = (CLKernel)_runtimeKernel!;");
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
            $"CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, {expression});");
    }

    protected override int EmitViewArg(
        LauncherEmissionContext ctx,
        string viewImplVar,
        string viewSourceExpr,
        int argIndex)
    {
        ctx.WriteLine(
            $"CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, (IntPtr){viewImplVar}.Ptr);");
        argIndex++;
        ctx.WriteLine(
            $"CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, {viewImplVar}.Length);");
        argIndex++;
        return argIndex;
    }

    protected override void EmitPointerArg(
        LauncherEmissionContext ctx,
        string expression,
        int argIndex)
    {
        ctx.WriteLine(
            $"CLAPI.CurrentAPI.SetKernelArgument(" +
            $"clKernel.KernelPtr, {argIndex}, (IntPtr){expression});");
    }

    protected override void EmitPostamble(
        LauncherEmissionContext ctx,
        int nextArgIndex)
    {
        ctx.WriteLine("");
        ctx.WriteLine(
            "CLException.ThrowIfFailed(" +
            "CLAPI.CurrentAPI.LaunchKernelWithStreamBinding" +
            "<CLAPI.DefaultLaunchHandler>(" +
            "clStream, clKernel, launchConfig));");
    }
}
