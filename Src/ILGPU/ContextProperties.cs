// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ContextProperties.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace ILGPU;

/// <summary>
/// Internal flags to specify the behavior of automatic page locking.
/// </summary>
public enum PageLockingMode
{
    /// <summary>
    /// All automatic page-locking allocations are disabled.
    /// </summary>
    /// <remarks>
    /// This is the default setting.
    /// </remarks>
    Default,

    /// <summary>
    /// All implicit memory allocations are automatically page locked by default
    /// during transfer operations. Note that externally allocated buffers need to
    /// be page-locked explicitly by the user.
    /// </summary>
    Auto,

    /// <summary>
    /// All memory buffers are page-locked automatically during transfer operations.
    /// This also affects buffers allocated by the user.
    /// </summary>
    Aggressive,
}

/// <summary>
/// Defines global context specific properties.
/// </summary>
public class ContextProperties
{
    #region Instance

    /// <summary>
    /// Stores all context-specific extension properties.
    /// </summary>
    private readonly Dictionary<string, object> _extensionProperties = [];

    /// <summary>
    /// Constructs an empty instance.
    /// </summary>
    internal ContextProperties() { }

    /// <summary>
    /// Constructs an instance based on the given source properties.
    /// </summary>
    private ContextProperties(Dictionary<string, object> sourceProperties)
    {
        _extensionProperties = new(sourceProperties);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Defines which buffers should be automatically page locked by default.
    /// </summary>
    /// <remarks><see cref="PageLockingMode.Default"/> by default.</remarks>
    public PageLockingMode PageLockingMode { get; protected set; } =
        PageLockingMode.Default;

    /// <summary>
    /// Returns true if profiling is enabled on all streams.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableProfiling { get; protected set; }

    #endregion

    #region Methods

    /// <summary>
    /// Gets an extension property.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">
    /// The default value (if the key could not be found).
    /// </param>
    /// <returns>The retrieved value.</returns>
    public T GetExtensionProperty<T>(string key, T defaultValue)
        where T : struct =>
        _extensionProperties.TryGetValue(key, out var result)
        ? (T)result
        : defaultValue;

    /// <summary>
    /// Sets an extension property.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value to store.</param>
    protected void SetExtensionProperty<T>(string key, T value)
        where T : struct =>
        _extensionProperties[key] = value;

    /// <summary>
    /// Instantiates all properties by replacing the automatic detection modes
    /// with more specific enumeration values.
    /// </summary>
    internal ContextProperties InstantiateProperties() =>
        new(_extensionProperties)
        {
            PageLockingMode = PageLockingMode,
            EnableProfiling = EnableProfiling,
        };

    #endregion
}

