// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Extensions;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.Runtime;

/// <summary>
/// Flags for accelerator stream instances.
/// </summary>
[Flags]
public enum AcceleratorStreamFlags
{
    /// <summary>
    /// Specifies an accelerator stream that must be assumed to run in sync.
    /// </summary>
    None = 0,

    /// <summary>
    /// Specified an async stream to allow for full concurrency.
    /// </summary>
    Async = 1,
}

/// <summary>
/// Represents an abstract kernel stream for asynchronous processing.
/// </summary>
/// <remarks>Members of this class are not thread safe.</remarks>
[SuppressMessage(
    "Microsoft.Naming",
    "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
public abstract partial class AcceleratorStream : AcceleratorObject
{
    #region Instance

    private readonly Action _synchronizeAction;
    private readonly Dictionary<Guid, AcceleratorExtension> _extensionCache = [];

    /// <summary>
    /// Constructs a new accelerator stream.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="flags">The associated flags.</param>
    protected AcceleratorStream(Accelerator accelerator, AcceleratorStreamFlags flags)
        : base(accelerator)
    {
        _synchronizeAction = Synchronize;
        OptimalKernelSize = accelerator.OptimalKernelSize;
        NumWarpsPerOptimalGroupSize = XMath.DivRoundUp(
            OptimalKernelSize.GroupSize,
            accelerator.WarpSize);
        WarpSize = accelerator.WarpSize;
        Flags = flags;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated flags.
    /// </summary>
    public AcceleratorStreamFlags Flags { get; }

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to launch common grid-stride
    /// loop kernels.
    /// </summary>
    public KernelSize OptimalKernelSize { get; }

    /// <summary>
    /// Returns the warp size of the underlying accelerator.
    /// </summary>
    public int WarpSize { get; }

    /// <summary>
    /// Returns the number of warps for kernel launches using
    /// <see cref="OptimalKernelSize"/>.
    /// </summary>
    public int NumWarpsPerOptimalGroupSize { get; }

    #endregion

    #region Methods
    /// <summary>
    /// Synchronizes all queued operations.
    /// </summary>
    public abstract void Synchronize();

    /// <summary>
    /// Synchronizes all queued operations asynchronously.
    /// </summary>
    /// <returns>A task object to wait for.</returns>
    public Task SynchronizeAsync() => Task.Run(_synchronizeAction);

    /// <summary>
    /// Makes the associated accelerator the current one for this thread and
    /// returns a <see cref="ScopedAcceleratorBinding"/> object that allows
    /// to easily recover the old binding.
    /// </summary>
    /// <returns>A scoped binding object.</returns>
    public ScopedAcceleratorBinding BindScoped() => Accelerator.AsNotNull().BindScoped();

    /// <summary>
    /// Adds a profiling marker into the stream.
    /// </summary>
    /// <returns>The profiling marker.</returns>
    public ProfilingMarker AddProfilingMarker() =>
        Accelerator.AsNotNull().Context.Properties.EnableProfiling
        ? AddProfilingMarkerInternal()
        : throw new NotSupportedException(
            RuntimeErrorMessages.NotSupportedProfilingMarker);

    /// <summary>
    /// Adds a profiling marker into the stream.
    /// </summary>
    /// <returns>The profiling marker.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract ProfilingMarker AddProfilingMarkerInternal();

    /// <summary>
    /// Gets a registered accelerator extension.
    /// </summary>
    /// <typeparam name="T">The extension type.</typeparam>
    /// <returns>The extension instance.</returns>
    public T GetExtension<T>() where T : IAcceleratorExtension
    {
        if (!_extensionCache.TryGetValue(T.Id, out var extension))
        {
            extension = Accelerator.AsNotNull().GetAcceleratorExtension<T>();
            _extensionCache.Add(T.Id, extension);
        }
        return extension.GetAsAbstractExtension<T>();
    }

    /// <summary>
    /// Computes number of groups with optimal group size.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <returns>The number of groups to launch.</returns>
    public long ComputeNumGroupsWithOptimalGroupSize(long numDataElements) =>
        numDataElements < 1
        ? throw new ArgumentOutOfRangeException(nameof(numDataElements))
        : OptimalKernelSize.ComputeNumGroups(numDataElements);

    /// <summary>
    /// Computes a kernel config using optimal group size.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <returns>The kernel configuration for launch.</returns>
    public KernelConfig ComputeKernelConfig(long numDataElements) => new(
        ComputeNumGroupsWithOptimalGroupSize(numDataElements),
        OptimalKernelSize.GroupSize);

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to launch common grid-stride
    /// loop kernels.
    /// </summary>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <param name="numIterationsPerGroup">
    /// The number of loop iterations per group.
    /// </param>
    public KernelConfig ComputeGridStrideKernelConfig(
        long numDataElements,
        out int numIterationsPerGroup) =>
        OptimalKernelSize.ComputeGridStrideKernelConfig(
            numDataElements,
            out numIterationsPerGroup);

    #endregion
}
