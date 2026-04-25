// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Availability.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Metal;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.ROCm;
using ILGPUC.Backends;
using ILGPUC.Compilers;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Cached compiler detection — detects available native compilers once per process.
/// Supports both local and remote compiler services via
/// <see cref="CompilerManagerFactory"/>.
/// </summary>
static class Availability
{
    private static readonly ConcurrentDictionary<BackendType, Lazy<Task<bool>>>
        s_availability = new();

    private static readonly ConcurrentDictionary<BackendType, Lazy<bool>>
        s_runtimeAvailability = new();

    /// <summary>
    /// Returns true if a native compiler is available for the given backend.
    /// CPU always returns true (no external compiler needed).
    /// Uses <see cref="CompilerManagerFactory"/> to resolve local or remote
    /// compiler managers per backend.
    /// </summary>
    public static async Task<bool> IsCompilerAvailableAsync(BackendType backend)
    {
        if (backend == BackendType.CPU)
            return true;

        var target = MapToTarget(backend);
        if (target is null)
            return false;

        var lazy = s_availability.GetOrAdd(backend, bt =>
            new Lazy<Task<bool>>(async () =>
            {
                var bttarget = MapToTarget(bt);
                if (bttarget is null)
                    return false;
                var manager = await CompilerManagerFactory
                    .ResolveAsync(bttarget.Value).ConfigureAwait(false);
                var caps = await manager.GetCapabilitiesAsync(
                    CancellationToken.None).ConfigureAwait(false);
                return caps.Compilers.Any(
                    c => c.Target == bttarget.Value && c.Available);
            }));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <summary>
    /// Maps the given backend type to a compilation target. Internal so
    /// <see cref="CompilerManagerFactory"/> can reuse the same mapping for
    /// its docker-default probe path.
    /// </summary>
    /// <param name="backend">The backend type.</param>
    internal static CompilationTarget? MapToTarget(BackendType backend) => backend switch
    {
        BackendType.Cuda => CompilationTarget.Cuda,
        BackendType.ROCm => CompilationTarget.Hip,
        BackendType.Metal => CompilationTarget.Metal,
        BackendType.OpenCL => CompilationTarget.OpenCLIntel,
        BackendType.CPU => null,
        _ => null,
    };

    /// <summary>
    /// Returns true if a usable runtime device exists for the given backend
    /// (e.g. an NVIDIA GPU for CUDA, an AMD GPU for ROCm, an Apple GPU for
    /// Metal, an OpenCL platform for OpenCL). CPU is always available.
    /// </summary>
    /// <remarks>
    /// This is a separate gate from <see cref="IsCompilerAvailableAsync"/>:
    /// the compiler check passes when nvcc/hipcc/etc. is reachable (locally
    /// or via a remote service / Docker container), which is enough for
    /// <c>BackendTests.NativeCompilation</c>. <see cref="ExecutionTests"/>
    /// additionally need a real device to launch the compiled kernel against,
    /// so they call this method too. Without this gate, GPU execution tests
    /// would crash with an opaque exit-code-134 process abort on machines
    /// that have a Docker-based compiler but no local GPU (e.g. macOS
    /// running the CUDA / ROCm containers).
    ///
    /// The probe calls each backend's <c>XxxDevice.GetDevices()</c>, which
    /// internally invokes the runtime's device enumeration API and is
    /// exception-safe — a missing driver, missing device, or mismatched
    /// platform yields an empty list rather than throwing. Result is cached
    /// per backend per process.
    /// </remarks>
    public static bool IsRuntimeAvailable(BackendType backend)
    {
        if (backend == BackendType.CPU)
            return true;

        var lazy = s_runtimeAvailability.GetOrAdd(backend, static bt =>
            new Lazy<bool>(() => ProbeRuntime(bt)));
        return lazy.Value;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The probe must never throw — any failure means the "
            + "runtime is unavailable on this machine.")]
    private static bool ProbeRuntime(BackendType backend)
    {
        try
        {
            return backend switch
            {
                BackendType.Cuda => CudaDevice.GetDevices(_ => true).Length > 0,
                BackendType.ROCm => ROCmDevice.GetDevices().Length > 0,
                BackendType.Metal => MetalDevice.GetDevices().Length > 0,
                BackendType.OpenCL => CLDevice.GetDevices(_ => true).Length > 0,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }
}
