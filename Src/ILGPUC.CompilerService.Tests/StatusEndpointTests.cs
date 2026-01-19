// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StatusEndpointTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net.Http.Json;
using System.Text.Json;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

public class StatusEndpointTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;

    public StatusEndpointTests(MockServiceFixture fixture)
        => _client = fixture.CreateClient();

    [Fact]
    public async Task Status_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/api/v1/status");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Status_ReturnsServiceName()
    {
        var response = await _client.GetAsync("/api/v1/status");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ILGPUC.CompilerService",
            json.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Status_ReturnsTimestamp()
    {
        var response = await _client.GetAsync("/api/v1/status");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("timestamp", out _));
    }
}
