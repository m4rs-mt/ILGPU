// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDispatchRegistry.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime;

/// <summary>
/// Connects generic kernel launches in one assembly with kernels compiled by the
/// build of another assembly.
/// </summary>
/// <remarks>
/// A library that launches a kernel from public generic code only compiles the
/// instantiations it uses itself. An application that calls the library with other
/// type arguments compiles the missing instantiations in its own build and registers
/// a launcher for each of them here; the library's generated dispatch code looks the
/// launcher up when it has no kernel of its own. This class is used by code that
/// ILGPU generates and is not meant to be called directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class KernelDispatchRegistry
{
    private static readonly ConcurrentDictionary<(string Key, Type Type), Delegate>
        _launchers = new();

    /// <summary>
    /// Registers a launcher for the dispatch point <paramref name="key"/>. The type
    /// of the launcher identifies the type arguments it was compiled for.
    /// </summary>
    /// <param name="key">The dispatch key, <c>{assembly}:{dispatch class}</c>.</param>
    /// <param name="launcher">The launcher.</param>
    public static void Register(string key, Delegate launcher)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(launcher);
        _launchers[(key, launcher.GetType())] = launcher;
    }

    /// <summary>
    /// Returns the launcher of type <typeparamref name="TDelegate"/> registered for
    /// the dispatch point <paramref name="key"/>, if any.
    /// </summary>
    /// <typeparam name="TDelegate">The launcher type.</typeparam>
    /// <param name="key">The dispatch key.</param>
    /// <param name="launcher">The launcher, if one is registered.</param>
    /// <returns>True if a launcher is registered.</returns>
    public static bool TryGetLauncher<TDelegate>(
        string key,
        [NotNullWhen(true)] out TDelegate? launcher)
        where TDelegate : Delegate
    {
        launcher = _launchers.TryGetValue((key, typeof(TDelegate)), out var registered)
            ? (TDelegate)registered
            : null;
        return launcher is not null;
    }
}

/// <summary>
/// Describes, for the build of other assemblies, which kernel instantiations the
/// public generic methods of this assembly launch. Written by the ILGPU build and
/// read by the builds of assemblies that reference this one.
/// </summary>
/// <param name="summary">The description in the ILGPU build's JSON format.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class KernelDispatchSummaryAttribute(string summary) : Attribute
{
    /// <summary>
    /// Returns the description in the ILGPU build's JSON format.
    /// </summary>
    public string Summary { get; } = summary;
}
