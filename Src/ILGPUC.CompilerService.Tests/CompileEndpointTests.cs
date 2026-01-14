// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompileEndpointTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

public class CompileEndpointTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;

    public CompileEndpointTests(MockServiceFixture fixture)
        => _client = fixture.CreateClient();

    [Theory]
    [InlineData(CompilationTarget.Cuda)]
    [InlineData(CompilationTarget.Hip)]
    [InlineData(CompilationTarget.Metal)]
    [InlineData(CompilationTarget.OpenCLIntel)]
    [InlineData(CompilationTarget.OpenCLAmd)]
    public async Task Compile_EachTarget_ReturnsSuccess(CompilationTarget target)
    {
        var request = new CompileRequest
        {
            SourceCode = $"// test kernel for {target}",
            Target = target,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Output);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(target, result.Target);
    }

    [Fact]
    public async Task Compile_DeterministicOutput()
    {
        var source = $"deterministic test {Guid.NewGuid()}";
        var request = new CompileRequest
        {
            SourceCode = source,
            Target = CompilationTarget.Cuda,
        };

        var r1 = await _client.PostAsJsonAsync("/api/v1/compile", request);
        var result1 = await r1.Content.ReadFromJsonAsync<CompilationResult>();

        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var expectedOutput = Convert.ToBase64String(expectedBytes);

        Assert.Equal(expectedOutput, result1!.Output);
    }

    [Fact]
    public async Task Compile_WithFlags_Succeeds()
    {
        var request = new CompileRequest
        {
            SourceCode = "// kernel with flags",
            Target = CompilationTarget.Cuda,
            CudaFlags = "-arch=sm_80",
            IncludePaths = ["/usr/local/include"],
            CompilerDirectives = ["DEBUG", "NDEBUG"],
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CompilationResult>();
        Assert.True(result!.Success);
    }
}
