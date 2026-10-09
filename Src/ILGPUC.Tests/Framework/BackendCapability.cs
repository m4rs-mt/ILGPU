// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BackendCapability.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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

    /// <summary>
    /// Float64 atomic operations (built-in <c>Atomic.Add</c> for double and
    /// the custom-CAS path that <c>Atomic.MakeAtomic</c> lowers to).
    /// Distinct from <see cref="Float64"/> arithmetic — Metal lacks a
    /// Float64-atomic emitter path entirely, and OpenCL's CAS over Int64
    /// requires the <c>cl_khr_int64_base_atomics</c> extension.
    /// </summary>
    Float64Atomics = 1 << 2,
}

/// <summary>
/// Annotates a kernel method with capabilities that aren't inferable from
/// its parameter / return types — for example, Float64 atomics, which are
/// indistinguishable from plain <c>double</c> arithmetic at the signature
/// level. Picked up by <c>KernelRegistry.InferCapabilities</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresCapabilityAttribute : Attribute
{
    public RequiresCapabilityAttribute(BackendCapability capability)
    {
        Capability = capability;
    }

    public BackendCapability Capability { get; }
}

/// <summary>
/// Marks a kernel method as known-failing on one or more backends —
/// typically because of an open compiler bug whose fix is tracked
/// elsewhere. Tests that consult this attribute (e.g. <c>BackendTests</c>'
/// <c>NativeCompilation</c> theory) skip the affected backend rows with
/// the attribute's <see cref="Reason"/> message. Removing the attribute
/// when the underlying bug is fixed re-arms the tests; that's the holding
/// pen the test framework provides for in-flight bugs.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class KnownFailingOnAttribute : Attribute
{
    public KnownFailingOnAttribute(params BackendType[] backends)
    {
        Backends = backends;
    }

    public BackendType[] Backends { get; }

    public string Reason { get; init; } =
        "Known failing — see attribute site for tracking.";
}

/// <summary>
/// Declares which capabilities each backend supports. Used by the test framework
/// to skip tests that require unsupported capabilities.
/// </summary>
public static class BackendCapabilities
{
    private static readonly Dictionary<BackendType, BackendCapability> s_supported = new()
    {
        [BackendType.CPU] = BackendCapability.Float64
            | BackendCapability.Float16
            | BackendCapability.Float64Atomics,
        [BackendType.Cuda] = BackendCapability.Float64
            | BackendCapability.Float16
            | BackendCapability.Float64Atomics,
        [BackendType.ROCm] = BackendCapability.Float64
            | BackendCapability.Float16
            | BackendCapability.Float64Atomics,
        [BackendType.OpenCL] = BackendCapability.Float64
            | BackendCapability.Float16,
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
