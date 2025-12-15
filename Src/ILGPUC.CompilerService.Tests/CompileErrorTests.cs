// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompileErrorTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

public class CompileErrorTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;

    public CompileErrorTests(MockServiceFixture fixture)
        => _client = fixture.CreateClient();

    [Fact]
    public async Task Compile_EmptySource_Returns400()
    {
        var request = new CompileRequest
        {
            SourceCode = "",
            Target = CompilationTarget.Cuda,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MISSING_SOURCE",
            json.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Compile_WhitespaceSource_Returns400()
    {
        var request = new CompileRequest
        {
            SourceCode = "   ",
            Target = CompilationTarget.Cuda,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Compile_InvalidTarget_Returns400()
    {
        var json = """{"sourceCode": "test", "target": 999}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/compile", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
