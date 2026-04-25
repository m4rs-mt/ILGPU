// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompositeCompilerManagerTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Mocks;

namespace ILGPUC.CompilerService.Tests;

/// <summary>
/// Unit tests for <see cref="CompositeCompilerManager"/>'s routing-table
/// behaviour. Uses <see cref="MockCompilerManager"/>s as inner managers so
/// the tests run without any HTTP, toolchain, or filesystem dependency.
/// </summary>
public class CompositeCompilerManagerTests
{
    [Fact]
    public async Task CompileAsync_RoutesToTargetSpecificManager()
    {
        var cuda = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Cuda]
        };
        var hip = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Hip]
        };

        var composite = new CompositeCompilerManager(
            new Dictionary<CompilationTarget, ICompilerManager>
            {
                [CompilationTarget.Cuda] = cuda,
                [CompilationTarget.Hip] = hip,
            });

        var cudaResult = await composite.CompileAsync(
            new CompileRequest
            {
                SourceCode = "// cuda",
                Target = CompilationTarget.Cuda,
            },
            CancellationToken.None);

        var hipResult = await composite.CompileAsync(
            new CompileRequest
            {
                SourceCode = "// hip",
                Target = CompilationTarget.Hip,
            },
            CancellationToken.None);

        Assert.Equal(1, cuda.CompileCallCount);
        Assert.Equal(1, hip.CompileCallCount);
        Assert.True(cudaResult.Success);
        Assert.True(hipResult.Success);
        Assert.Equal(CompilationTarget.Cuda, cudaResult.Target);
        Assert.Equal(CompilationTarget.Hip, hipResult.Target);
    }

    [Fact]
    public async Task CompileAsync_UnroutedTarget_ReturnsExplicitFailure()
    {
        var cuda = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Cuda]
        };

        var composite = new CompositeCompilerManager(
            new Dictionary<CompilationTarget, ICompilerManager>
            {
                [CompilationTarget.Cuda] = cuda,
            });

        var result = await composite.CompileAsync(
            new CompileRequest
            {
                SourceCode = "// metal — not routed",
                Target = CompilationTarget.Metal,
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, cuda.CompileCallCount);
        Assert.Contains("Metal", result.StdErr);
        Assert.Equal(CompilationTarget.Metal, result.Target);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_MergesPerRouteCapabilities()
    {
        var cuda = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Cuda]
        };
        var hip = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Hip]
        };
        var metal = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Metal]
        };

        var composite = new CompositeCompilerManager(
            new Dictionary<CompilationTarget, ICompilerManager>
            {
                [CompilationTarget.Cuda] = cuda,
                [CompilationTarget.Hip] = hip,
                [CompilationTarget.Metal] = metal,
            });

        var caps = await composite.GetCapabilitiesAsync(CancellationToken.None);

        Assert.Equal(3, caps.Compilers.Length);
        Assert.Contains(caps.Compilers,
            c => c.Target == CompilationTarget.Cuda && c.Available);
        Assert.Contains(caps.Compilers,
            c => c.Target == CompilationTarget.Hip && c.Available);
        Assert.Contains(caps.Compilers,
            c => c.Target == CompilationTarget.Metal && c.Available);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_PassesThroughInnerAvailability()
    {
        // Inner manager advertises Cuda but the routing table maps Hip to it.
        // The composite faithfully reports what the inner says about Hip
        // (Available=false), rather than dropping the entry. Consumers that
        // care only about reachable backends should filter on Available.
        var cudaOnly = new MockCompilerManager
        {
            AvailableTargets = [CompilationTarget.Cuda]
        };

        var composite = new CompositeCompilerManager(
            new Dictionary<CompilationTarget, ICompilerManager>
            {
                [CompilationTarget.Hip] = cudaOnly,
            });

        var caps = await composite.GetCapabilitiesAsync(CancellationToken.None);

        var hip = Assert.Single(caps.Compilers);
        Assert.Equal(CompilationTarget.Hip, hip.Target);
        Assert.False(hip.Available);
    }

    [Fact]
    public void Routes_ExposedAsReadOnly()
    {
        var inner = new MockCompilerManager();
        var composite = new CompositeCompilerManager(
            new Dictionary<CompilationTarget, ICompilerManager>
            {
                [CompilationTarget.Cuda] = inner,
            });

        Assert.Single(composite.Routes);
        Assert.Same(inner, composite.Routes[CompilationTarget.Cuda]);
    }
}
