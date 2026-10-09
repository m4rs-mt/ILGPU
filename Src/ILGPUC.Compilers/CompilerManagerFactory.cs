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

using System.Collections.Concurrent;

namespace ILGPUC.Compilers;

/// <summary>
/// Resolves <see cref="ICompilerManager"/> instances against a chain of
/// configuration sources: explicit URIs (probed for capabilities), per-target
/// environment variables, a global environment variable, per-target Docker
/// default ports, and finally the local toolchain.
/// </summary>
/// <remarks>
/// This is the single discovery path shared by the ILGPUC CLI and the test
/// framework. The chain is:
/// <list type="number">
/// <item>
/// Each explicit <see cref="Uri"/> passed to
/// <see cref="CreateAsync(IReadOnlyList{Uri}, bool, CancellationToken)"/> is
/// queried via <see cref="ICompilerManager.GetCapabilitiesAsync"/>; the first
/// URI to advertise a target wins for that target.
/// </item>
/// <item>
/// Per-target environment variables (<c>ILGPU_CUDA_SERVICE_URL</c>,
/// <c>ILGPU_ROCM_SERVICE_URL</c>, <c>ILGPU_METAL_SERVICE_URL</c>,
/// <c>ILGPU_OPENCL_SERVICE_URL</c>) — trusted, no probe.
/// </item>
/// <item>
/// Global environment variable <c>ILGPU_COMPILER_SERVICE_URL</c> — trusted,
/// no probe; covers any unrouted target.
/// </item>
/// <item>
/// Per-target Docker default ports (5001 / 5002 / 5003) — probed with a
/// 3-second timeout. Defaults match <c>Src/docker/run.sh</c>; bump both files
/// together when reassigning.
/// </item>
/// <item>
/// Local <see cref="CompilerManager"/> — only included when
/// <c>allowLocal</c> is set on the request.
/// </item>
/// </list>
/// </remarks>
public static class CompilerManagerFactory
{
    // Default Docker-container URLs for the per-target probe path. These
    // duplicate the host ports in Src/docker/run.sh
    // (CUDA_HOST_PORT / ROCM_HOST_PORT / OPENCL_HOST_PORT). Bump both files
    // together when reassigning ports.
    private const string DefaultCudaServiceUrl = "http://localhost:5001";
    private const string DefaultRocmServiceUrl = "http://localhost:5002";
    private const string DefaultOpenCLServiceUrl = "http://localhost:5003";

    // Upper bound on how long the docker-default probe is allowed to take.
    // A failed TCP connect on localhost is near-instant; this only matters
    // if the container is up but its capabilities endpoint is hung.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly Dictionary<CompilationTarget, string> s_envVarNames = new()
    {
        [CompilationTarget.Cuda] = "ILGPU_CUDA_SERVICE_URL",
        [CompilationTarget.Hip] = "ILGPU_ROCM_SERVICE_URL",
        [CompilationTarget.Metal] = "ILGPU_METAL_SERVICE_URL",
        [CompilationTarget.OpenCLIntel] = "ILGPU_OPENCL_SERVICE_URL",
        [CompilationTarget.OpenCLAmd] = "ILGPU_OPENCL_SERVICE_URL",
    };

    private static readonly ConcurrentDictionary<string, RemoteCompilerManager>
        s_remoteManagers = new();

    private static readonly Lazy<CompilerManager> s_localManager =
        new(() => new CompilerManager());

    // Per-target cache of the resolved manager for the no-explicit-URI path.
    // Each target's probe runs at most once per process; the chosen manager
    // is reused thereafter.
    private static readonly ConcurrentDictionary<
        CompilationTarget, Lazy<Task<ICompilerManager>>> s_resolved = new();

    /// <summary>
    /// Returns a single <see cref="ICompilerManager"/> for the given target,
    /// honouring (in order) per-target env vars, the global env var, per-target
    /// Docker default ports, and the local <see cref="CompilerManager"/> as
    /// fallback. The result is cached per target per process.
    /// </summary>
    /// <param name="target">The compilation target to resolve.</param>
    /// <param name="ct">A token to cancel the probe.</param>
    /// <returns>
    /// A manager that can serve <paramref name="target"/>, or the local manager
    /// if no remote service is reachable.
    /// </returns>
    public static Task<ICompilerManager> ResolveAsync(
        CompilationTarget target,
        CancellationToken ct = default)
        => s_resolved.GetOrAdd(target, t =>
            new Lazy<Task<ICompilerManager>>(() =>
                ResolveSingleAsync(t, ct))).Value;

    /// <summary>
    /// Builds a <see cref="CompositeCompilerManager"/> by walking the full
    /// resolution chain. <paramref name="explicitUris"/> are probed first so
    /// callers (e.g. the CLI's <c>--compiler-service</c> flag) can override
    /// env-var configuration.
    /// </summary>
    /// <param name="explicitUris">
    /// User-supplied service URIs in declaration order. Each is queried for
    /// capabilities; the first URI to advertise a target wins.
    /// </param>
    /// <param name="allowLocal">
    /// When <see langword="true"/>, the local <see cref="CompilerManager"/>
    /// fills any target not covered by a remote service.
    /// </param>
    /// <param name="ct">A token to cancel the probes.</param>
    /// <returns>
    /// A composite manager mapping each resolvable
    /// <see cref="CompilationTarget"/> to a concrete inner manager. Targets
    /// with no available manager are simply absent from the routing table —
    /// dispatching to one returns an explicit failure result.
    /// </returns>
    public static async Task<CompositeCompilerManager> CreateAsync(
        IReadOnlyList<Uri>? explicitUris = null,
        bool allowLocal = true,
        CancellationToken ct = default)
    {
        var routes = new Dictionary<CompilationTarget, ICompilerManager>();
        var allTargets = Enum.GetValues<CompilationTarget>();

        // 1. Explicit URIs — probe each, first to advertise a target wins.
        if (explicitUris is not null)
        {
            foreach (var uri in explicitUris)
            {
                var remote = GetOrCreateRemote(uri.ToString());
                var caps = await ProbeCapabilitiesAsync(remote, ct).ConfigureAwait(false);
                if (caps is null)
                    continue;
                foreach (var entry in caps.Compilers)
                {
                    if (!entry.Available)
                        continue;
                    routes.TryAdd(entry.Target, remote);
                }
            }
        }

        // 2. Per-target env vars — trusted, no probe.
        foreach (var target in allTargets)
        {
            if (routes.ContainsKey(target))
                continue;
            if (s_envVarNames.TryGetValue(target, out var envName))
            {
                var url = Environment.GetEnvironmentVariable(envName);
                if (!string.IsNullOrEmpty(url))
                    routes[target] = GetOrCreateRemote(url);
            }
        }

        // 3. Global env var — trusted, no probe; covers any unrouted target.
        var globalUrl = Environment.GetEnvironmentVariable("ILGPU_COMPILER_SERVICE_URL");
        if (!string.IsNullOrEmpty(globalUrl))
        {
            var remote = GetOrCreateRemote(globalUrl);
            foreach (var target in allTargets)
                routes.TryAdd(target, remote);
        }

        // 4. Per-target Docker default port probe.
        foreach (var target in allTargets)
        {
            if (routes.ContainsKey(target))
                continue;
            var defaultUrl = GetDefaultDockerUrl(target);
            if (defaultUrl is null)
                continue;
            var remote = GetOrCreateRemote(defaultUrl);
            if (await IsRemoteUsableAsync(remote, target, ct).ConfigureAwait(false))
                routes[target] = remote;
        }

        // 5. Local fallback.
        if (allowLocal)
        {
            var local = s_localManager.Value;
            foreach (var target in allTargets)
                routes.TryAdd(target, local);
        }

        return new CompositeCompilerManager(routes);
    }

    /// <summary>
    /// Returns a <see cref="RemoteCompilerManager"/> for the given URL,
    /// reusing the same instance for repeated calls so the underlying
    /// <see cref="HttpClient"/> is shared.
    /// </summary>
    public static RemoteCompilerManager GetOrCreateRemote(string url) =>
        s_remoteManagers.GetOrAdd(url, u => new RemoteCompilerManager(new Uri(u)));

    /// <summary>
    /// Returns the local <see cref="CompilerManager"/> singleton.
    /// </summary>
    public static CompilerManager GetLocalManager() => s_localManager.Value;

    private static async Task<ICompilerManager> ResolveSingleAsync(
        CompilationTarget target,
        CancellationToken ct)
    {
        // 1. Explicit per-target env var → trust the user, no probe.
        if (s_envVarNames.TryGetValue(target, out var envName))
        {
            var url = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(url))
                return GetOrCreateRemote(url);
        }

        // 2. Explicit global env var → trust the user, no probe.
        var globalUrl = Environment.GetEnvironmentVariable("ILGPU_COMPILER_SERVICE_URL");
        if (!string.IsNullOrEmpty(globalUrl))
            return GetOrCreateRemote(globalUrl);

        // 3. Per-target Docker default — probe + fall back.
        var defaultUrl = GetDefaultDockerUrl(target);
        if (defaultUrl is not null)
        {
            var remote = GetOrCreateRemote(defaultUrl);
            if (await IsRemoteUsableAsync(remote, target, ct).ConfigureAwait(false))
                return remote;
        }

        // 4. Local CompilerManager.
        return s_localManager.Value;
    }

    private static string? GetDefaultDockerUrl(CompilationTarget target) => target switch
    {
        CompilationTarget.Cuda => DefaultCudaServiceUrl,
        CompilationTarget.Hip => DefaultRocmServiceUrl,
        CompilationTarget.OpenCLIntel or CompilationTarget.OpenCLAmd =>
            DefaultOpenCLServiceUrl,
        _ => null,
    };

    private static async Task<bool> IsRemoteUsableAsync(
        ICompilerManager remote,
        CompilationTarget target,
        CancellationToken ct)
    {
        var caps = await ProbeCapabilitiesAsync(remote, ct).ConfigureAwait(false);
        if (caps is null)
            return false;
        return caps.Compilers.Any(
            c => c.Target == target && c.Available);
    }

    private static async Task<CompilationCapabilities?> ProbeCapabilitiesAsync(
        ICompilerManager remote,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);
        try
        {
            return await remote.GetCapabilitiesAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // RemoteCompilerManager already swallows exceptions internally,
            // but belt & braces in case of cancellation propagation.
            return null;
        }
    }
}
