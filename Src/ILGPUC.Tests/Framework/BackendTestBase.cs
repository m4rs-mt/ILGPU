// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BackendTestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Backends;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Base class for Stage 2 (backend code generation + native compile) tests.
/// GPU backends skip if the native compiler is not found.
/// </summary>
public abstract class BackendTestBase : DisposeBase
{
    private readonly CompilationHelper _helper;
    private readonly ITestOutputHelper _output;

    protected BackendType Backend { get; }

    protected BackendTestBase(ITestOutputHelper output, BackendType backend)
    {
        _output = output;
        Backend = backend;
        _helper = new CompilationHelper(backend);
    }

    /// <summary>
    /// Assert source generation succeeds (non-empty source, non-empty entry
    /// point). If the kernel requires backend capabilities this backend
    /// doesn't support (Float64 / Float64Atomics / Float16), skips instead.
    /// </summary>
    protected void AssertSourceGenerationSucceeds(
        MethodInfo kernel,
        CompilationProperties? props = null,
        BackendCapability required = BackendCapability.None)
    {
        var unsupported = BackendCapabilities.GetUnsupported(Backend, required);
        Skip.If(
            unsupported != BackendCapability.None,
            $"{Backend} does not support: {unsupported}");

        var result = _helper.GenerateBackendCode(kernel, Backend, props);
        _output.WriteLine($"// Entry point: {result.EntryPointName}");
        _output.WriteLine(result.SourceCode);

        Assert.False(
            string.IsNullOrWhiteSpace(result.SourceCode),
            "Source code generation produced empty output");
        Assert.False(
            string.IsNullOrWhiteSpace(result.EntryPointName),
            "Entry point name is empty");
    }

    /// <summary>
    /// Assert native compilation succeeds (skips if compiler unavailable,
    /// the kernel requires capabilities this backend does not support, or
    /// the kernel is annotated with <see cref="KnownFailingOnAttribute"/>
    /// for the current backend).
    /// </summary>
    protected async Task AssertNativeCompilationSucceeds(
        MethodInfo kernel,
        CompilationProperties? props = null,
        BackendCapability required = BackendCapability.None,
        KnownFailingOnAttribute? knownFailing = null)
    {
        // Skip if kernel is a known-failing holding pen for this backend
        if (knownFailing is not null
            && Array.IndexOf(knownFailing.Backends, Backend) >= 0)
        {
            Skip.If(true,
                $"Known failing on {Backend}: {knownFailing.Reason}");
        }

        // Skip if kernel requires unsupported capabilities
        var unsupported = BackendCapabilities.GetUnsupported(Backend, required);
        Skip.If(
            unsupported != BackendCapability.None,
            $"{Backend} does not support: {unsupported}");

        var available = await Availability.IsCompilerAvailableAsync(Backend);
        Skip.IfNot(available, $"Native compiler for {Backend} is not available");

        var result = await _helper.CompileToNativeAsync(kernel, Backend, props);

        Assert.NotNull(result);
        Assert.True(
            result!.Success,
            $"Native compilation failed: {result.StdErr}");
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

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}
