// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDelegates.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace ILGPU.Runtime;

/// <summary>
/// Associates the delegates passed to methods launching kernels with the kernels
/// ILGPUC compiled for them. ILGPUC registers every lambda and static method
/// passed to such a method at its call site, and the rewritten launch site
/// selects the kernel compiled for the target of the delegate it receives.
/// </summary>
/// <remarks>
/// Calls to these methods are inserted by ILGPUC and are not meant to be used
/// directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class KernelDelegates
{
    /// <summary>
    /// Maps each registered delegate target to its keys.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, string[]> _keys = new();

    /// <summary>
    /// Registers the target of the given delegate under the given key.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate type.</typeparam>
    /// <param name="target">The delegate to register.</param>
    /// <param name="key">The key of the kernels compiled for its target.</param>
    /// <returns>The given delegate.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TDelegate Register<TDelegate>(TDelegate target, string key)
        where TDelegate : Delegate
    {
        var method = target.Method;
        if (_keys.TryGetValue(method, out var keys) && Array.IndexOf(keys, key) >= 0)
            return target;
        _keys.AddOrUpdate(
            method,
            static (_, newKey) => [newKey],
            static (_, existing, newKey) => Array.IndexOf(existing, newKey) >= 0
                ? existing
                : [.. existing, newKey],
            key);
        return target;
    }

    /// <summary>
    /// Returns true if the target of the given delegate has been registered
    /// under the given key.
    /// </summary>
    /// <param name="target">The delegate to check.</param>
    /// <param name="key">The key of the kernels compiled for a target.</param>
    /// <returns>True if the target has been registered under the key.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool IsRegistered(Delegate? target, string key) =>
        target is not null &&
        target.HasSingleTarget &&
        _keys.TryGetValue(target.Method, out var keys) &&
        Array.IndexOf(keys, key) >= 0;

    /// <summary>
    /// Binds a delegate parameter of a kernel to the target registered under
    /// the given key. ILGPUC resolves calls to this method while compiling
    /// kernels, it cannot be called at runtime.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate type.</typeparam>
    /// <param name="key">The key the target is registered under.</param>
    /// <returns>The bound delegate.</returns>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called at runtime.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TDelegate Bind<TDelegate>(string key)
        where TDelegate : Delegate =>
        throw new NotSupportedException(ErrorMessages.NotSupportedKernelDelegateBind);
}
