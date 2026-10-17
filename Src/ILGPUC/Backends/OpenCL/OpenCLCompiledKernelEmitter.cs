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
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// OpenCL-specific compiled kernel emitter that generates classes deriving from
/// CLCompiledKernel.
/// </summary>
sealed partial class OpenCLCompiledKernelEmitter(
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

    /// <remarks>
    /// The capabilities are derived from the generated OpenCL C source, which
    /// names exactly the optional features the device driver must provide.
    /// Without them, the accelerator would try to build a kernel that uses
    /// e.g. <c>double</c> on a device without fp64 and fail.
    /// </remarks>
    public override void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx)
    {
        var source = ctx.KernelSourceCode ?? string.Empty;
        var required = new List<string>();
        if (DoubleRegex().IsMatch(source))
            required.Add("Float64 = true");
        if (HalfRegex().IsMatch(source))
            required.Add("Float16 = true");
        if (Int64AtomicsRegex().IsMatch(source))
            required.Add("Int64Atomics = true");
        if (SubGroupsRegex().IsMatch(source))
            required.Add("SubGroups = true");

        ctx.WriteLine("public override AcceleratorCapabilities RequiredCapabilities =>");
        ctx.WriteLine(required.Count == 0
            ? "    new CLAcceleratorCapabilities();"
            : $"    new CLAcceleratorCapabilities {{ {string.Join(", ", required)} }};");
    }

    [GeneratedRegex(@"\bdouble\d*\b")]
    private static partial Regex DoubleRegex();

    [GeneratedRegex(@"\bhalf\d*\b")]
    private static partial Regex HalfRegex();

    // 64-bit atomics are emitted as the cl_khr_int64_*_atomics atom_* functions
    [GeneratedRegex(@"\batom_\w+\s*\(")]
    private static partial Regex Int64AtomicsRegex();

    [GeneratedRegex(@"\b(intel_)?sub_group_\w+\s*\(|\bget_(max_)?sub_group_\w+\s*\(")]
    private static partial Regex SubGroupsRegex();
}
