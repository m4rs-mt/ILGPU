// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BackendCapability.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using System;
using System.Collections.Generic;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Flags representing hardware/backend capabilities required by a kernel or test.
/// </summary>
[Flags]
public enum BackendCapability
{
    None = 0,

    /// <summary>Double precision (float64) arithmetic.</summary>
    Float64 = 1 << 0,

    /// <summary>Half precision (float16) arithmetic.</summary>
    Float16 = 1 << 1,
}

/// <summary>
/// Declares which capabilities each backend supports. Used by the test framework
/// to skip tests that require unsupported capabilities.
/// </summary>
public static class BackendCapabilities
{
    private static readonly Dictionary<BackendType, BackendCapability> s_supported = new()
    {
        [BackendType.CPU] = BackendCapability.Float64 | BackendCapability.Float16,
        [BackendType.Cuda] = BackendCapability.Float64 | BackendCapability.Float16,
        [BackendType.ROCm] = BackendCapability.Float64 | BackendCapability.Float16,
        [BackendType.OpenCL] = BackendCapability.Float64 | BackendCapability.Float16,
        [BackendType.Metal] = BackendCapability.Float16,
    };

    /// <summary>
    /// Returns true if <paramref name="backend"/> supports all of the requested
    /// <paramref name="required"/> capabilities.
    /// </summary>
    public static bool Supports(BackendType backend, BackendCapability required) =>
        required == BackendCapability.None
        || (s_supported.TryGetValue(backend, out var supported)
            && (supported & required) == required);

    /// <summary>
    /// Returns the capabilities NOT supported by <paramref name="backend"/>
    /// from the <paramref name="required"/> set.
    /// </summary>
    public static BackendCapability GetUnsupported(
        BackendType backend, BackendCapability required)
    {
        if (!s_supported.TryGetValue(backend, out var supported))
            return required;
        return required & ~supported;
    }
}
