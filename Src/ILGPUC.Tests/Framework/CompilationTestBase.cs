// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationTestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.IR;
using System;
using System.Reflection;
using Xunit.Abstractions;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Base class for Stage 1 (IR snapshot) tests. Always runs — no hardware dependency.
/// </summary>
public abstract class CompilationTestBase(ITestOutputHelper output) : DisposeBase
{
    private static readonly bool DumpIR =
        Environment.GetEnvironmentVariable("ILGPU_DUMP_IR") == "1";

    private readonly CompilationHelper _helper = new();

    private void WriteIR(string ir)
    {
        if (DumpIR)
            output.WriteLine(ir);
    }

    /// <summary>
    /// Verify AfterFrontend IR (opt-invariant, backend-invariant).
    /// </summary>
    protected void VerifyAfterFrontend(MethodInfo kernel)
    {
        var ir = _helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);
        WriteIR(ir);

        var (className, methodName) = GetKernelNames(kernel);
        var snapshotPath = IRSnapshotVerifier.GetSnapshotPath(
            className, methodName, IRDumpPoint.AfterFrontend);
        IRSnapshotVerifier.VerifyOrUpdate(ir, snapshotPath);
    }

    /// <summary>
    /// Verify AfterGlobalOpt at specific opt level and compilation mode.
    /// </summary>
    protected void VerifyAfterGlobalOpt(
        MethodInfo kernel, OptimizationLevel opt, CompilationMode mode)
    {
        var props = new CompilationProperties(OptimizationLevel: opt).WithMode(mode);
        var ir = _helper.GetNormalizedIR(kernel, IRDumpPoint.AfterGlobalOpt, props);
        WriteIR(ir);

        var (className, methodName) = GetKernelNames(kernel);
        var snapshotPath = IRSnapshotVerifier.GetSnapshotPath(
            className, methodName, IRDumpPoint.AfterGlobalOpt, opt, mode: mode);
        IRSnapshotVerifier.VerifyOrUpdate(ir, snapshotPath);
    }

    /// <summary>
    /// Verify AfterBackendTransforms at specific backend, opt level, and compilation mode.
    /// </summary>
    protected void VerifyAfterBackendTransforms(
        MethodInfo kernel, BackendType backend,
        OptimizationLevel opt, CompilationMode mode)
    {
        var props = new CompilationProperties(OptimizationLevel: opt).WithMode(mode);
        var ir = _helper.GetNormalizedIR(
            kernel, IRDumpPoint.AfterBackendTransforms, props, backend);
        WriteIR(ir);

        var (className, methodName) = GetKernelNames(kernel);
        var snapshotPath = IRSnapshotVerifier.GetSnapshotPath(
            className, methodName, IRDumpPoint.AfterBackendTransforms,
            opt, backend, mode);
        IRSnapshotVerifier.VerifyOrUpdate(ir, snapshotPath);
    }

    /// <summary>
    /// Resolve kernel method by name from a kernel class.
    /// </summary>
    protected static MethodInfo GetKernel(Type kernelClass, string name) =>
        kernelClass.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new ArgumentException(
            $"Method '{name}' not found on type '{kernelClass.Name}'");

    /// <summary>
    /// Resolve a generic kernel method, making it concrete with the given type argument.
    /// </summary>
    protected static MethodInfo GetKernel<T>(Type kernelClass, string name) =>
        GetGenericKernel(kernelClass, name, typeof(T));

    /// <summary>
    /// Resolve a generic kernel method, making it concrete with the given type argument.
    /// </summary>
    protected static MethodInfo GetGenericKernel(
        Type kernelClass, string name, Type typeArg) =>
        (kernelClass.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new ArgumentException(
            $"Method '{name}' not found on type '{kernelClass.Name}'"))
        .MakeGenericMethod(typeArg);

    private static (string ClassName, string MethodName) GetKernelNames(MethodInfo kernel)
    {
        var className = kernel.DeclaringType?.Name ?? "Unknown";
        var methodName = kernel.IsGenericMethod
            ? $"{kernel.Name}.{string.Join(
                "_",
                Array.ConvertAll(kernel.GetGenericArguments(), t => t.Name))}"
            : kernel.Name;
        return (className, methodName);
    }

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}
