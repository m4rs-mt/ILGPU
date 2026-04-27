// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RemoteCompilerIntegrationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.CompilerService.Tests.Fixtures;

namespace ILGPUC.CompilerService.Tests;

/// <summary>
/// Tests the full round-trip: <see cref="RemoteCompilerManager"/> → HTTP →
/// CompilerService → <see cref="Mocks.MockCompilerManager"/> → response back
/// through HTTP → <see cref="RemoteCompilerManager"/>.
/// </summary>
public class RemoteCompilerIntegrationTests : IClassFixture<MockServiceFixture>
{
    private readonly MockServiceFixture _fixture;

    public RemoteCompilerIntegrationTests(MockServiceFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task RemoteManager_CompileAsync_FullRoundTrip()
    {
        var httpClient = _fixture.CreateClient();
        var manager = new RemoteCompilerManager(
            httpClient.BaseAddress!, httpClient);

        var request = new CompileRequest
        {
            SourceCode = $"// full round-trip test {Guid.NewGuid()}",
            Target = CompilationTarget.Cuda,
        };

        var result = await manager.CompileAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal(CompilationTarget.Cuda, result.Target);
    }

    [Fact]
    public async Task RemoteManager_GetCapabilities_FullRoundTrip()
    {
        var httpClient = _fixture.CreateClient();
        var manager = new RemoteCompilerManager(
            httpClient.BaseAddress!, httpClient);

        var caps = await manager.GetCapabilitiesAsync(CancellationToken.None);

        Assert.NotNull(caps);
        Assert.Equal(5, caps.Compilers.Length);
        Assert.All(caps.Compilers, c => Assert.True(c.Available));
    }

    [Fact]
    public async Task RemoteManager_CompileAllTargets()
    {
        var httpClient = _fixture.CreateClient();
        var manager = new RemoteCompilerManager(
            httpClient.BaseAddress!, httpClient);

        foreach (var target in Enum.GetValues<CompilationTarget>())
        {
            var request = new CompileRequest
            {
                SourceCode = $"// test for {target} {Guid.NewGuid()}",
                Target = target,
            };

            var result = await manager.CompileAsync(request, CancellationToken.None);
            Assert.True(result.Success, $"Failed for target {target}: {result.StdErr}");
        }
    }
}
