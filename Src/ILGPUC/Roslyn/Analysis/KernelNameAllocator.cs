// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelNameAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Assigns unique kernel names. Every generated kernel class is called
/// <c>{name}_CompiledKernel</c> and written to a file of the same name, so two
/// different kernels must never share a name, not even in a different case.
/// </summary>
/// <remarks>
/// Kernels keep their preferred name (usually the kernel method's name) unless
/// another kernel prefers the same name. Colliding kernels are distinguished by a
/// readable suffix, such as their type arguments or their launch variant, and by
/// a stable hash of their key if that is still not enough.
/// </remarks>
sealed class KernelNameAllocator
{
    /// <summary>
    /// A request for a name.
    /// </summary>
    /// <param name="Key">Identifies the kernel; equal keys share a name.</param>
    /// <param name="Preferred">The preferred name.</param>
    /// <param name="Disambiguator">
    /// A readable suffix used when the preferred name collides.
    /// </param>
    private sealed record NameRequest(string Key, string Preferred, string Disambiguator);

    /// <summary>
    /// The maximum length of a name. Names of deeply nested type arguments can grow
    /// without bound, and they become class and file names.
    /// </summary>
    public const int MaxNameLength = 96;

    /// <summary>
    /// The length of the readable prefix kept when a name is shortened.
    /// </summary>
    private const int ShortenedPrefixLength = 72;

    private readonly List<NameRequest> _requests = [];
    private readonly Dictionary<string, string> _namesByKey = [];
    private readonly HashSet<string> _usedNames =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Requests a name for the kernel identified by <paramref name="key"/>.
    /// Names are assigned by <see cref="Resolve"/>.
    /// </summary>
    public void Request(string key, string preferred, string disambiguator)
    {
        if (_requests.All(r => r.Key != key))
            _requests.Add(new NameRequest(key, preferred, disambiguator));
    }

    /// <summary>
    /// Assigns names to all requests made so far.
    /// </summary>
    public void Resolve()
    {
        foreach (var group in _requests.GroupBy(
                     r => r.Preferred,
                     StringComparer.OrdinalIgnoreCase))
        {
            var requests = group.ToList();
            foreach (var request in requests)
            {
                var name = requests.Count == 1
                    ? request.Preferred
                    : Combine(request.Preferred, request.Disambiguator);
                Assign(request.Key, name);
            }
        }
        _requests.Clear();
    }

    /// <summary>
    /// Assigns a name derived from <paramref name="preferred"/> to
    /// <paramref name="key"/> right away.
    /// </summary>
    public string Allocate(string key, string preferred) =>
        _namesByKey.TryGetValue(key, out var existing)
            ? existing
            : Assign(key, preferred);

    /// <summary>
    /// Returns the name assigned to <paramref name="key"/>.
    /// </summary>
    public string this[string key] => _namesByKey[key];

    /// <summary>
    /// Assigns <paramref name="name"/>, or a hashed variant of it if it is taken.
    /// </summary>
    private string Assign(string key, string name)
    {
        if (name.Length > MaxNameLength)
        {
            name = Combine(
                name[..ShortenedPrefixLength].TrimEnd('_'),
                GenericContext.GetStableHash(key));
        }
        if (!_usedNames.Add(name))
        {
            name = Combine(name, GenericContext.GetStableHash(key));
            for (int i = 2; !_usedNames.Add(name); ++i)
                name = Combine(name, i.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        _namesByKey[key] = name;
        return name;
    }

    /// <summary>
    /// Appends a suffix to a name.
    /// </summary>
    private static string Combine(string name, string suffix) =>
        suffix.Length > 0 ? $"{name}_{suffix}" : name;
}
