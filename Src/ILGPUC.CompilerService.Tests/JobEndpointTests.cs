// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: JobEndpointTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

public class JobEndpointTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;

    public JobEndpointTests(MockServiceFixture fixture)
        => _client = fixture.CreateClient();

    [Fact]
    public async Task AsyncCompile_ReturnsJobId()
    {
        var request = new CompileRequest
        {
            SourceCode = $"// async test {Guid.NewGuid()}",
            Target = CompilationTarget.Cuda,
            AsyncMode = true,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("jobId", out var jobId));
        Assert.False(string.IsNullOrEmpty(jobId.GetString()));
    }

    [Fact]
    public async Task AsyncCompile_JobCompletesWithResult()
    {
        var request = new CompileRequest
        {
            SourceCode = $"// async complete test {Guid.NewGuid()}",
            Target = CompilationTarget.Metal,
            AsyncMode = true,
        };

        var enqueue = await _client.PostAsJsonAsync("/api/v1/compile", request);
        var enqueueJson = await enqueue.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = enqueueJson.GetProperty("jobId").GetString()!;

        // Poll until completed (mock is fast — should complete almost immediately)
        CompilationResult? result = null;
        for (int i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var poll = await _client.GetAsync($"/api/v1/jobs/{jobId}");
            var pollJson = await poll.Content.ReadFromJsonAsync<JsonElement>();
            var status = pollJson.GetProperty("status").GetString();

            if (status is "completed" or "failed")
            {
                if (pollJson.TryGetProperty("result", out var resultProp))
                    result = resultProp.Deserialize<CompilationResult>();
                break;
            }
        }

        Assert.NotNull(result);
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task GetJob_NonexistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/jobs/nonexistent-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AsyncCompile_WithCustomJobId()
    {
        var customId = $"custom-{Guid.NewGuid():N}";
        var request = new CompileRequest
        {
            SourceCode = $"// custom job id test {Guid.NewGuid()}",
            Target = CompilationTarget.Hip,
            AsyncMode = true,
            JobId = customId,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/compile", request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(customId, json.GetProperty("jobId").GetString());

        // Verify we can retrieve by custom ID
        var poll = await _client.GetAsync($"/api/v1/jobs/{customId}");
        poll.EnsureSuccessStatusCode();
    }
}
