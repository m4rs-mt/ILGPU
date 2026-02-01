// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ICache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;

namespace ILGPUC.CompilerService.Caching;

/// <summary>
/// Represents a cache for compilation results.
/// </summary>
public interface ICache
{
    /// <summary>
    /// Retrieves a cached compilation result by key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>
    /// The cached result, or <see langword="null"/> if not found.
    /// </returns>
    CompilationResult? Get(string key);

    /// <summary>
    /// Stores a compilation result in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="result">The compilation result to cache.</param>
    void Set(string key, CompilationResult result);
}
