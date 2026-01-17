// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CachingTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net.Http.Json;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;
using ILGPUC.CompilerService.Tests.Mocks;

namespace ILGPUC.CompilerService.Tests;

public class CachingTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;
    private readonly MockCompilerManager _mock;

    public CachingTests(MockServiceFixture fixture)
    {
        _client = fixture.CreateClient();
        _mock = fixture.MockManager;
    }

    [Fact]
    public async Task Compile_SameSourceTwice_SecondIsCached()
    {
        var request = new CompileRequest
        {
            SourceCode = $"// cache test {Guid.NewGuid()}",
            Target = CompilationTarget.Metal,
        };

        var countBefore = _mock.CompileCallCount;

        // First request — cache miss
        var r1 = await _client.PostAsJsonAsync("/api/v1/compile", request);
        var result1 = await r1.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.True(result1!.Success);
        Assert.False(result1.IsCached);

        // Second request — cache hit
        var r2 = await _client.PostAsJsonAsync("/api/v1/compile", request);
        var result2 = await r2.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.True(result2!.Success);
        Assert.True(result2.IsCached);

        // Mock was called only once (second was served from cache)
        Assert.Equal(countBefore + 1, _mock.CompileCallCount);
    }

    [Fact]
    public async Task Compile_DifferentSource_NotCached()
    {
        var unique = Guid.NewGuid().ToString();

        var r1 = await _client.PostAsJsonAsync("/api/v1/compile",
            new CompileRequest
            {
                SourceCode = $"source A {unique}",
                Target = CompilationTarget.Cuda,
            });
        var result1 = await r1.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.False(result1!.IsCached);

        var r2 = await _client.PostAsJsonAsync("/api/v1/compile",
            new CompileRequest
            {
                SourceCode = $"source B {unique}",
                Target = CompilationTarget.Cuda,
            });
        var result2 = await r2.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.False(result2!.IsCached);
    }

    [Fact]
    public async Task Compile_SameSourceDifferentTarget_NotCached()
    {
        var source = $"// multi-target {Guid.NewGuid()}";

        var r1 = await _client.PostAsJsonAsync("/api/v1/compile",
            new CompileRequest
            {
                SourceCode = source,
                Target = CompilationTarget.Cuda,
            });
        var result1 = await r1.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.False(result1!.IsCached);

        // Same source, different target — should be a cache miss
        var r2 = await _client.PostAsJsonAsync("/api/v1/compile",
            new CompileRequest
            {
                SourceCode = source,
                Target = CompilationTarget.Metal,
            });
        var result2 = await r2.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.False(result2!.IsCached);
    }
}
