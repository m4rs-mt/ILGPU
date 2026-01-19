// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MockCompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using ILGPUC.Compilers;

namespace ILGPUC.CompilerService.Tests.Mocks;

/// <summary>
/// A fake <see cref="ICompilerManager"/> that returns deterministic results
/// without invoking any real compiler toolchain. Used to test the service
/// infrastructure (HTTP, caching, jobs) in isolation from platform dependencies.
/// </summary>
public sealed class MockCompilerManager : ICompilerManager
{
    private int _compileCallCount;

    /// <summary>
    /// The set of targets this mock reports as available.
    /// Defaults to all 5 targets.
    /// </summary>
    public HashSet<CompilationTarget> AvailableTargets { get; init; } =
    [
        CompilationTarget.Cuda,
        CompilationTarget.Hip,
        CompilationTarget.Metal,
        CompilationTarget.OpenCLIntel,
        CompilationTarget.OpenCLAmd,
    ];

    /// <summary>
    /// If set, <see cref="CompileAsync"/> returns a failure result with this
    /// message in <see cref="CompilationResult.StdErr"/>.
    /// </summary>
    public string? ForceFailureMessage { get; set; }

    /// <summary>
    /// Tracks how many times <see cref="CompileAsync"/> has been called.
    /// Useful for verifying caching behavior (second call should not reach
    /// the manager if the cache is working).
    /// </summary>
    public int CompileCallCount => _compileCallCount;

    /// <inheritdoc/>
    public Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _compileCallCount);

        if (ForceFailureMessage is not null)
        {
            return Task.FromResult(new CompilationResult
            {
                Success = false,
                StdErr = ForceFailureMessage,
                ExitCode = 1,
                Target = request.Target,
                OutputType = request.OutputType,
            });
        }

        if (!AvailableTargets.Contains(request.Target))
        {
            return Task.FromResult(new CompilationResult
            {
                Success = false,
                StdErr = $"Mock: target {request.Target} not available",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            });
        }

        // Produce a deterministic mock binary: the source code hashed to
        // 32 bytes, so identical inputs always produce identical outputs
        // (important for cache-hit testing).
        var mockBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.SourceCode));
        var output = Convert.ToBase64String(mockBytes);

        return Task.FromResult(new CompilationResult
        {
            Success = true,
            Output = output,
            StdOut = "Mock compilation succeeded",
            ExitCode = 0,
            Target = request.Target,
            OutputType = request.OutputType,
        });
    }

    /// <inheritdoc/>
    public Task<CompilationCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        var compilers = Enum.GetValues<CompilationTarget>()
            .Select(target => new CompilerCapability
            {
                Target = target,
                Available = AvailableTargets.Contains(target),
                Version = AvailableTargets.Contains(target)
                    ? "Mock Compiler v1.0"
                    : null,
                ToolchainComponents =
                [
                    new ToolchainComponent
                    {
                        Name = "mock-compiler",
                        Available = AvailableTargets.Contains(target),
                        Version = "1.0",
                    }
                ],
            })
            .ToArray();

        return Task.FromResult(new CompilationCapabilities { Compilers = compilers });
    }
}
