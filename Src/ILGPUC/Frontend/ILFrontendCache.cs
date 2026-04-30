// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILFrontendCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend.DebugInformation;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ILGPUC.Frontend;

/// <summary>
/// Status of a disassembly attempt for a given method.
/// </summary>
internal enum DisassembleStatus
{
    /// <summary>
    /// <see cref="Disassembler.TryDisassemble"/> returned a non-null body.
    /// <see cref="DisassemblyCacheEntry.Body"/> is non-null.
    /// </summary>
    Disassembled,

    /// <summary>
    /// <see cref="Disassembler.TryDisassemble"/> either threw or returned
    /// <see langword="null"/> — the method has no parseable IL (BCL internal,
    /// unresolvable token, unsupported IL, etc.). Cached so warm compiles
    /// don't reattempt the work and re-throw. <see cref="DisassemblyCacheEntry.Body"/>
    /// is <see langword="null"/>.
    /// </summary>
    Failed,
}

/// <summary>
/// Cached disassembly result for a single method.
/// </summary>
internal readonly record struct DisassemblyCacheEntry(
    DisassembledMethod? Body,
    DisassembleStatus Status);

/// <summary>
/// Per-<see cref="ILGPUC.KernelCompiler"/> cache of frontend artifacts that
/// are safe to reuse across kernel compilations within a single compiler
/// instance:
///
/// <list type="bullet">
/// <item>
/// <description>Disassembled IL bodies — backend-independent, byte-identical
/// on every invocation, dominated by reflection cost on a cold compile and
/// reused for free on a warm compile.</description>
/// </item>
/// <item>
/// <description>Loaded portable PDB metadata — read-only post-load, expensive
/// to parse the first time (one stream open + metadata reader per assembly).
/// </description>
/// </item>
/// </list>
///
/// Lifetime is tied to the owning <see cref="ILGPUC.KernelCompiler"/>, which
/// in turn lives for one <c>ilgpuc compile</c> / <c>ilgpuc build</c> invocation
/// or one ILGPUC.CompilerService request — so memory is naturally bounded
/// and there is no <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
/// pinning concern from a process-static cache.
/// </summary>
/// <remarks>
/// We deliberately cache only the genuine output of
/// <c>Disassembler.TryDisassemble</c> keyed by the method that was actually
/// parsed. The intrinsic-remap aliasing applied inside <c>ILFrontend</c>
/// (where the source method's slot in <c>_methods</c> is backfilled with the
/// target method's body) stays per-instance so that future backend-specific
/// intrinsic implementations resolve correctly per backend.
/// </remarks>
internal sealed class ILFrontendCache
{
    /// <summary>
    /// Disassembled bodies keyed by the method that was actually parsed.
    /// </summary>
    public ConcurrentDictionary<MethodBase, DisassemblyCacheEntry> Disassembly
    { get; } = new(concurrencyLevel: Environment.ProcessorCount, capacity: 2048);

    /// <summary>
    /// Loaded PDB metadata keyed by assembly. Wrapped in <see cref="Lazy{T}"/>
    /// with <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/>
    /// so that concurrent first-time loads of the same assembly only open and
    /// parse the PDB once. A factory that returns <see langword="null"/>
    /// (missing PDB or open failure) caches <see langword="null"/>, matching
    /// the pre-cache behaviour of <c>_referencedAssemblies[assembly] = null</c>.
    /// </summary>
    public ConcurrentDictionary<Assembly, Lazy<AssemblyDebugInformation?>> Pdb
    { get; } = new(concurrencyLevel: Environment.ProcessorCount, capacity: 32);

    /// <summary>
    /// Drops every cached entry. Intended for tests and benchmarking only —
    /// production callers should let the cache live as long as the owning
    /// <see cref="ILGPUC.KernelCompiler"/>.
    /// </summary>
    public void Clear()
    {
        Disassembly.Clear();
        Pdb.Clear();
    }
}
