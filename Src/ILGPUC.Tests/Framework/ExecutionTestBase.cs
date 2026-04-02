// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExecutionTestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Backends;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Base class for execution tests. Builds test programs through the full ILGPUC
/// pipeline and verifies their stdout output.
/// </summary>
public abstract class ExecutionTestBase : DisposeBase
{
    private readonly ProgramBuilder _builder;
    private readonly ITestOutputHelper _output;

    protected BackendType Backend { get; }

    protected ExecutionTestBase(ITestOutputHelper output, BackendType backend)
    {
        _output = output;
        Backend = backend;
        _builder = new ProgramBuilder(backend, output);
    }

    /// <summary>
    /// Resolves a test program path relative to the test output directory.
    /// </summary>
    protected static string GetTestProgramPath(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Test program not found: {relativePath}", path);
        return path;
    }

    /// <summary>
    /// Skips the current test if the backend does not support all of the
    /// requested capabilities (e.g. Float64 on Metal).
    /// </summary>
    protected static void RequireCapability(
        BackendType backend, BackendCapability required)
    {
        var unsupported = BackendCapabilities.GetUnsupported(backend, required);
        Skip.If(
            unsupported != BackendCapability.None,
            $"{backend} does not support: {unsupported}");
    }

    /// <summary>
    /// Build and run a test program, returning the process result.
    /// </summary>
    protected async Task<ProcessResult> RunProgramAsync(
        string programSourceFile,
        string[] kernelMethodNames,
        CompilationProperties? props = null)
    {
        // Skip if native compiler unavailable (CPU always passes, GPU checks toolchain)
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(Backend).ConfigureAwait(false),
            $"Native compiler for {Backend} is not available on this machine");

        // Skip if no runtime device exists. The compiler check above can be
        // satisfied by a remote / Docker compiler service even on machines
        // with no local GPU; ExecutionTests additionally need a real device
        // to dispatch the compiled kernel against. Without this gate, GPU
        // tests on hardware-less hosts (e.g. macOS running the CUDA / ROCm
        // Docker containers) crash with an opaque exit-code-134 abort.
        Skip.IfNot(
            Availability.IsRuntimeAvailable(Backend),
            $"No {Backend} runtime device available on this machine");

        // CPU vectorized mode: disable assertions (bounds checks) because the
        // vectorized code generator does not yet handle cross-type comparisons
        // in debug-mode ArrayView bounds-check code.
        if (Backend == BackendType.CPU)
        {
            props ??= new CompilationProperties();
            props = props with { EnableAssertions = false };
        }

        var sourcePath = GetTestProgramPath(programSourceFile);
        var exePath = _builder.BuildExecutable(
            [sourcePath],
            kernelMethodNames,
            props);

        var result = await ProcessRunner.RunAsync(exePath);

        _output.WriteLine($"Exit code: {result.ExitCode}");
        _output.WriteLine($"Timed out: {result.TimedOut}");
        if (result.StdOutLines.Length > 0)
        {
            _output.WriteLine("Stdout:");
            foreach (var line in result.StdOutLines)
                _output.WriteLine($"  {line}");
        }
        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            _output.WriteLine("Stderr:");
            _output.WriteLine(result.StdErr);
        }

        return result;
    }

    /// <summary>
    /// Build, run, and verify a test program's stdout output.
    /// </summary>
    protected async Task VerifyProgramOutputAsync(
        string programSourceFile,
        string[] kernelMethodNames,
        string[] expectedOutput,
        CompilationProperties? props = null)
    {
        var result = await RunProgramAsync(
            programSourceFile, kernelMethodNames, props);

        Assert.False(result.TimedOut, "Test program timed out");
        Assert.Equal(0, result.ExitCode);
        OutputVerifier.Verify(result.StdOutLines, expectedOutput);
    }

    /// <summary>
    /// Build, run, and verify with float tolerance for GPU backends.
    /// </summary>
    protected async Task VerifyProgramOutputWithToleranceAsync(
        string programSourceFile,
        string[] kernelMethodNames,
        string[] expectedOutput,
        CompilationProperties? props = null)
    {
        var result = await RunProgramAsync(
            programSourceFile, kernelMethodNames, props);

        Assert.False(result.TimedOut, "Test program timed out");
        Assert.Equal(0, result.ExitCode);
        OutputVerifier.VerifyWithTolerance(result.StdOutLines, expectedOutput, Backend);
    }

    protected override void Dispose(bool disposing)
    {
        _builder.Dispose();
        base.Dispose(disposing);
    }
}
