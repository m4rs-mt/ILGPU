// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerManagerFactory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Compilers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Resolves environment variables into <see cref="ICompilerManager"/> instances.
/// Supports per-backend and global remote compiler service URLs, with a
/// probe-and-fall-back to the local Docker container ports for CUDA / ROCm
/// (see <see cref="DefaultCudaServiceUrl"/> / <see cref="DefaultRocmServiceUrl"/>),
/// and finally a local <see cref="CompilerManager"/>.
/// </summary>
static class CompilerManagerFactory
{
    // Default Docker-container URLs for the per-backend probe path. These
    // duplicate the host ports in Src/docker/run.sh
    // (CUDA_HOST_PORT / ROCM_HOST_PORT / OPENCL_HOST_PORT). Bump both files
    // together when reassigning ports.
    // See Src/plans/new_docker_containers.md for the full port table.
    private const string DefaultCudaServiceUrl = "http://localhost:5001";
    private const string DefaultRocmServiceUrl = "http://localhost:5002";
    private const string DefaultOpenCLServiceUrl = "http://localhost:5003";

    // Upper bound on how long the docker-default probe is allowed to take.
    // A failed TCP connect on localhost is near-instant; this only matters
    // if the container is up but its capabilities endpoint is hung.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly Dictionary<BackendType, string> s_envVarNames = new()
    {
        [BackendType.Cuda] = "ILGPU_CUDA_SERVICE_URL",
        [BackendType.ROCm] = "ILGPU_ROCM_SERVICE_URL",
        [BackendType.Metal] = "ILGPU_METAL_SERVICE_URL",
        [BackendType.OpenCL] = "ILGPU_OPENCL_SERVICE_URL",
    };

    private static readonly string? s_globalUrl =
        Environment.GetEnvironmentVariable("ILGPU_COMPILER_SERVICE_URL");

    private static readonly Dictionary<BackendType, string> s_perBackendUrls = BuildPerBackendUrls();

    private static readonly Lazy<CompilerManager> s_localManager = new(() => new CompilerManager());

    private static readonly ConcurrentDictionary<string, RemoteCompilerManager> s_remoteManagers = new();

    // Per-backend cache of the resolved manager. Each backend's probe runs
    // at most once per process; the chosen manager is reused thereafter.
    private static readonly ConcurrentDictionary<BackendType, Lazy<Task<ICompilerManager>>>
        s_resolved = new();

    /// <summary>
    /// Returns the appropriate <see cref="ICompilerManager"/> for the given
    /// backend, honouring env vars only. Does NOT probe the docker default
    /// URLs — kept for callers that cannot go async. Prefer
    /// <see cref="GetCompilerManagerAsync"/> in async contexts.
    /// </summary>
    public static ICompilerManager GetCompilerManager(BackendType backend)
    {
        if (s_perBackendUrls.TryGetValue(backend, out var url))
            return GetOrCreateRemote(url);

        if (s_globalUrl is not null)
            return GetOrCreateRemote(s_globalUrl);

        return s_localManager.Value;
    }

    /// <summary>
    /// Resolves the compiler manager to use for the given backend. Honours
    /// per-backend and global env vars first; otherwise probes the per-backend
    /// docker default URL (5001 for CUDA, 5002 for ROCm) via
    /// <see cref="ICompilerManager.GetCapabilitiesAsync"/> and uses the
    /// remote manager if the container reports the backend as available;
    /// otherwise falls back to the local <see cref="CompilerManager"/>.
    /// The resolution result is cached per backend per process — the probe
    /// runs at most once per <c>dotnet test</c> invocation.
    /// </summary>
    public static Task<ICompilerManager> GetCompilerManagerAsync(BackendType backend)
        => s_resolved.GetOrAdd(backend, bt =>
            new Lazy<Task<ICompilerManager>>(() => ResolveAsync(bt))).Value;

    private static async Task<ICompilerManager> ResolveAsync(BackendType backend)
    {
        // 1. Explicit per-backend env var → trust the user, no probe.
        if (s_perBackendUrls.TryGetValue(backend, out var url))
            return GetOrCreateRemote(url);

        // 2. Explicit global env var → trust the user, no probe.
        if (s_globalUrl is not null)
            return GetOrCreateRemote(s_globalUrl);

        // 3. Per-backend Docker default — probe + fall back.
        var defaultUrl = backend switch
        {
            BackendType.Cuda => DefaultCudaServiceUrl,
            BackendType.ROCm => DefaultRocmServiceUrl,
            BackendType.OpenCL => DefaultOpenCLServiceUrl,
            _ => null,
        };
        if (defaultUrl is not null)
        {
            var remote = GetOrCreateRemote(defaultUrl);
            if (await IsRemoteUsableAsync(remote, backend).ConfigureAwait(false))
                return remote;
        }

        // 4. Local CompilerManager (existing behaviour).
        return s_localManager.Value;
    }

    private static async Task<bool> IsRemoteUsableAsync(
        ICompilerManager remote, BackendType backend)
    {
        var target = Availability.MapToTarget(backend);
        if (target is null)
            return false;
        using var cts = new CancellationTokenSource(ProbeTimeout);
        try
        {
            var caps = await remote.GetCapabilitiesAsync(cts.Token)
                .ConfigureAwait(false);
            return caps.Compilers.Any(
                c => c.Target == target.Value && c.Available);
        }
        catch
        {
            // RemoteCompilerManager already swallows exceptions internally,
            // but belt & braces in case of cancellation propagation.
            return false;
        }
    }

    private static RemoteCompilerManager GetOrCreateRemote(string url) =>
        s_remoteManagers.GetOrAdd(url, u => new RemoteCompilerManager(new Uri(u)));

    private static Dictionary<BackendType, string> BuildPerBackendUrls()
    {
        var result = new Dictionary<BackendType, string>();
        foreach (var (backend, envVar) in s_envVarNames)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (value is not null)
                result[backend] = value;
        }
        return result;
    }
}
