// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CapabilitiesEndpointTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Net.Http.Json;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

public class CapabilitiesEndpointTests : IClassFixture<MockServiceFixture>
{
    private readonly HttpClient _client;

    public CapabilitiesEndpointTests(MockServiceFixture fixture)
        => _client = fixture.CreateClient();

    [Fact]
    public async Task Capabilities_ReturnsAllTargets()
    {
        var caps = await _client.GetFromJsonAsync<CompilationCapabilities>(
            "/api/v1/capabilities");

        Assert.NotNull(caps);
        Assert.Equal(5, caps!.Compilers.Length);
        Assert.All(caps.Compilers, c => Assert.True(c.Available));
    }

    [Fact]
    public async Task Capabilities_ReportsVersion()
    {
        var caps = await _client.GetFromJsonAsync<CompilationCapabilities>(
            "/api/v1/capabilities");

        Assert.All(caps!.Compilers,
            c => Assert.Equal("Mock Compiler v1.0", c.Version));
    }

    [Fact]
    public async Task Capabilities_IncludesToolchainComponents()
    {
        var caps = await _client.GetFromJsonAsync<CompilationCapabilities>(
            "/api/v1/capabilities");

        Assert.All(caps!.Compilers, c =>
        {
            Assert.NotNull(c.ToolchainComponents);
            Assert.Single(c.ToolchainComponents!);
            Assert.Equal("mock-compiler", c.ToolchainComponents![0].Name);
        });
    }
}
