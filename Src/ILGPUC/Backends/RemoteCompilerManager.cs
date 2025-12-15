// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RemoteCompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1031 // Do not catch general exception types

namespace ILGPUC.Backends;

/// <summary>
/// An <see cref="ICompilerManager"/> that forwards compilation requests to a remote
/// ILGPUC.CompilerService instance over HTTP.
/// </summary>
sealed class RemoteCompilerManager : ICompilerManager
{
    private const string CompileEndpoint = "api/v1/compile";
    private const string CapabilitiesEndpoint = "api/v1/capabilities";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new <see cref="RemoteCompilerManager"/> that sends requests to
    /// the given base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL of the remote compiler service.</param>
    public RemoteCompilerManager(Uri baseUrl)
        : this(baseUrl, new HttpClient())
    { }

    /// <summary>
    /// Initializes a new <see cref="RemoteCompilerManager"/> with an explicit
    /// <see cref="HttpClient"/> instance (useful for testing or DI).
    /// </summary>
    /// <param name="baseUrl">The base URL of the remote compiler service.</param>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    public RemoteCompilerManager(Uri baseUrl, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = baseUrl;
    }

    /// <inheritdoc/>
    public async Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {

        try
        {
            var response = await _httpClient
                .PostAsJsonAsync(CompileEndpoint, request, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<CompilationResult>(ct)
                .ConfigureAwait(false);

            return result ?? new CompilationResult
            {
                Success = false,
                StdErr = "Remote compiler service returned an empty response.",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                StdErr = $"Remote compiler service request failed: {ex.Message}",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<CompilationCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        try
        {
            var capabilities = await _httpClient
                .GetFromJsonAsync<CompilationCapabilities>(CapabilitiesEndpoint, ct)
                .ConfigureAwait(false);

            return capabilities ?? new CompilationCapabilities { Compilers = [] };
        }
        catch
        {
            return new CompilationCapabilities { Compilers = [] };
        }
    }
}

#pragma warning restore CA1031 // Do not catch general exception types
