// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompositeCompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// An <see cref="ICompilerManager"/> that dispatches each <see cref="CompileRequest"/>
/// to a per-target inner manager.
/// </summary>
/// <remarks>
/// Used to compose a single logical manager from a mix of remote compiler services
/// and a local <see cref="CompilerManager"/> — for example, when a build host has
/// no GPU toolchains installed but can reach one or more <c>ILGPUC.CompilerService</c>
/// instances that each advertise a subset of the supported targets.
/// </remarks>
public sealed class CompositeCompilerManager : ICompilerManager
{
    private readonly IReadOnlyDictionary<CompilationTarget, ICompilerManager> _routes;

    /// <summary>
    /// Initializes a new <see cref="CompositeCompilerManager"/> from a routing
    /// table that maps each supported <see cref="CompilationTarget"/> to the
    /// inner <see cref="ICompilerManager"/> that should serve it.
    /// </summary>
    /// <param name="routes">
    /// Per-target manager routes. Targets not present in the dictionary are
    /// reported as unavailable in <see cref="GetCapabilitiesAsync"/> and produce
    /// an explicit failure <see cref="CompilationResult"/> when targeted by
    /// <see cref="CompileAsync"/>.
    /// </param>
    public CompositeCompilerManager(
        IReadOnlyDictionary<CompilationTarget, ICompilerManager> routes)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
    }

    /// <summary>
    /// Returns the per-target routing table this manager dispatches against.
    /// </summary>
    public IReadOnlyDictionary<CompilationTarget, ICompilerManager> Routes => _routes;

    /// <inheritdoc/>
    public async Task<CompilationResult> CompileAsync(
        CompileRequest request, CancellationToken ct)
    {
        if (!_routes.TryGetValue(request.Target, out var inner))
        {
            return new CompilationResult
            {
                Success = false,
                StdErr = $"No compiler service is registered for target "
                    + $"'{request.Target}'.",
                ExitCode = -1,
                Target = request.Target,
                OutputType = request.OutputType,
            };
        }
        return await inner.CompileAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns merged capabilities by querying each routed inner manager and
    /// keeping only the entry whose <see cref="CompilerCapability.Target"/>
    /// matches the routing-table key. This produces one capability per route,
    /// even if an inner manager advertises additional targets that this
    /// composite has not been asked to serve.
    /// </summary>
    public async Task<CompilationCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        var caps = new List<CompilerCapability>(_routes.Count);
        foreach (var (target, manager) in _routes)
        {
            var inner = await manager.GetCapabilitiesAsync(ct).ConfigureAwait(false);
            var match = inner.Compilers.FirstOrDefault(c => c.Target == target);
            if (match is not null)
                caps.Add(match);
        }
        return new CompilationCapabilities { Compilers = [.. caps] };
    }
}
