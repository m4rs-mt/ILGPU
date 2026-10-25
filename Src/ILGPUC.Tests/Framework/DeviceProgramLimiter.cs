// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeviceProgramLimiter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Limits the number of test programs that run on the same GPU backend at the
/// same time.
/// </summary>
/// <remarks>
/// Every test program creates its own accelerator, and a GPU accelerator
/// reserves device memory for its context (about 250 MiB per CUDA program,
/// most of it in the driver). With xUnit running one test per core, a full
/// run would otherwise hold dozens of contexts at once, which exhausts the
/// device memory of shared or unified-memory GPUs. Running more programs at
/// once does not make a run faster either, as the driver serializes context
/// creation. Only the program processes are limited: building them still runs
/// fully in parallel, and the timeout of a program starts once it runs. CPU
/// programs are never limited.
///
/// The limit defaults to <see cref="DefaultLimit"/> and can be set through the
/// <c>ILGPU_TEST_DEVICE_PROGRAMS</c> environment variable; a value of zero or
/// less removes the limit.
/// </remarks>
static class DeviceProgramLimiter
{
    /// <summary>
    /// The environment variable that sets the limit per GPU backend.
    /// </summary>
    public const string LimitVariable = "ILGPU_TEST_DEVICE_PROGRAMS";

    /// <summary>
    /// The default number of programs running on the same GPU backend.
    /// </summary>
    public const int DefaultLimit = 4;

    private static readonly Lazy<int> s_limit = new(ResolveLimit);

    private static readonly ConcurrentDictionary<BackendType, SemaphoreSlim>
        s_semaphores = new();

    /// <summary>
    /// Returns the number of programs that may run on the same GPU backend at
    /// the same time, or zero if the number is not limited.
    /// </summary>
    public static int Limit => s_limit.Value;

    /// <summary>
    /// Runs the given program once a slot of its backend is free.
    /// </summary>
    /// <param name="backend">The backend the program runs on.</param>
    /// <param name="dllPath">The path of the program to run.</param>
    public static async Task<ProcessResult> RunAsync(
        BackendType backend,
        string dllPath)
    {
        if (backend == BackendType.CPU || Limit < 1)
            return await ProcessRunner.RunAsync(dllPath).ConfigureAwait(false);

        var semaphore = s_semaphores.GetOrAdd(
            backend,
            static _ => new SemaphoreSlim(Limit, Limit));
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ProcessRunner.RunAsync(dllPath).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static int ResolveLimit()
    {
        var value = Environment.GetEnvironmentVariable(LimitVariable);
        if (string.IsNullOrWhiteSpace(value))
            return DefaultLimit;
        if (!int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var limit))
        {
            throw new InvalidOperationException(
                $"{LimitVariable} must be an integer, but is '{value}'");
        }
        return Math.Max(limit, 0);
    }
}
