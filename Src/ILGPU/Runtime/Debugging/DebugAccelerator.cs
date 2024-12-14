// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.CPU;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1508 // Avoid dead conditional code

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// The accelerator mode to be used with the <see cref="DebugAccelerator"/>.
/// </summary>
public enum DebugAccelerationMode
{
    /// <summary>
    /// The automatic mode uses <see cref="Sequential"/> if a debugger is attached.
    /// It uses <see cref="Parallel"/> if no debugger is attached to the
    /// application.
    /// </summary>
    /// <remarks>
    /// This is the default mode.
    /// </remarks>
    Auto = 0,

    /// <summary>
    /// If the CPU accelerator uses a simulated sequential execution mechanism. This
    /// is particularly useful to simplify debugging. Note that different threads for
    /// distinct multiprocessors may still run in parallel.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// A parallel execution mode that runs all execution threads in parallel. This
    /// reduces processing time but makes it harder to use a debugger.
    /// </summary>
    Parallel = 2,
}

/// <summary>
/// Represents a general CPU-based debugger for kernels.
/// </summary>
public sealed class DebugAccelerator : Accelerator
{
    #region Instance

    [SuppressMessage(
        "Microsoft.Usage",
        "CA2213: Disposable fields should be disposed",
        Justification = "This is disposed in DisposeAccelerator_SyncRoot")]
    private readonly SemaphoreSlim _taskConcurrencyLimit = new(1);

    /// <summary>
    /// Constructs a new CPU runtime.
    /// </summary>
    /// <param name="context">The ILGPU context.</param>
    /// <param name="description">The accelerator description.</param>
    /// <param name="mode">The current accelerator mode.</param>
    /// <param name="threadPriority">
    /// The thread priority of the execution threads.
    /// </param>
    internal DebugAccelerator(
        Context context,
        DebugDevice description,
        DebugAccelerationMode mode,
        ThreadPriority threadPriority)
        : base(context, description)
    {
        NativePtr = new IntPtr(1);

        DefaultStream = CreateStreamInternal(AcceleratorStreamFlags.None);

        NumThreads = description.NumThreads;
        Mode = mode;
        ThreadPriority = threadPriority;
        UsesSequentialExecution =
            Mode == DebugAccelerationMode.Sequential ||
            Mode == DebugAccelerationMode.Auto && Debugger.IsAttached;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the current mode.
    /// </summary>
    public DebugAccelerationMode Mode { get; }

    /// <summary>
    /// Returns the current thread priority.
    /// </summary>
    public ThreadPriority ThreadPriority { get; }

    /// <summary>
    /// Returns true if the current accelerator uses a simulated sequential execution
    /// mechanism. This is particularly useful to simplify debugging. Note that
    /// different threads for distinct multiprocessors may still run in parallel.
    /// </summary>
    public bool UsesSequentialExecution { get; }

    /// <summary>
    /// Returns the number of threads.
    /// </summary>
    public int NumThreads { get; }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override MemoryBuffer AllocateRawInternal(
        long length,
        int elementSize) =>
        CPUMemoryBuffer.Create(length, elementSize);

    /// <inheritdoc/>
    protected override Kernel LoadKernel(CompiledKernel compiledKernel) =>
        new DebugKernel(this, compiledKernel);

    /// <inheritdoc/>
    protected override AcceleratorStream CreateStreamInternal(
        AcceleratorStreamFlags flags) =>
        new DebugStream(this);

    /// <summary cref="Accelerator.Synchronize"/>
    protected override void SynchronizeInternal() { }

    /// <summary cref="Accelerator.OnBind"/>
    protected override void OnBind() { }

    /// <summary cref="Accelerator.OnUnbind"/>
    protected override void OnUnbind() { }

    #endregion

    #region Peer Access

    /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
        otherAccelerator is DebugAccelerator;

    /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
    {
        if (!CanAccessPeerInternal(otherAccelerator))
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
        }
    }

    /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
    protected override void DisablePeerAccessInternal(
        Accelerator otherAccelerator) =>
        Debug.Assert(
            CanAccessPeerInternal(otherAccelerator),
            "Invalid EnablePeerAccess method");

    #endregion

    #region Execution Methods

    /// <summary>
    /// Launches the given kernel on this debug accelerator configuration.
    /// </summary>
    /// <param name="kernelConfig">The current kernel configuration.</param>
    /// <param name="kernel">The kernel to launch.</param>
    /// <param name="context">The kernel context.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Will be raised in case of invalid launch configurations.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void LaunchKernel(
        in KernelConfig kernelConfig,
        DebugKernel kernel,
        DebugKernelContext context)
    {
        if (kernelConfig.GroupSize > MaxNumThreadsPerGroup)
            throw new ArgumentOutOfRangeException(nameof(kernelConfig));

        _taskConcurrencyLimit.Wait();
        try
        {
            if (UsesSequentialExecution)
            {
                for (long i = 0; i < kernelConfig.GridSize; ++i)
                    kernel.InvokeGroup(kernelConfig, i, context);
            }
            else
            {
                var localConfig = kernelConfig;
                Parallel.For(0, kernelConfig.GridSize, i =>
                    kernel.InvokeGroup(localConfig, i, context));
            }
        }
        finally
        {
            _taskConcurrencyLimit.Release();
        }
    }

    #endregion

    #region Occupancy

    /// <inheritdoc />
    protected internal override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
        Kernel kernel,
        int groupSize,
        int dynamicSharedMemorySizeInBytes) =>
        kernel is DebugKernel
        ? NumThreads / groupSize
        : throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

    /// <inheritdoc />
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not DebugKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        // Estimation
        minGridSize = NumThreads;
        return 1;
    }

    /// <inheritdoc />
    protected internal override int EstimateGroupSizeInternal(
        Kernel kernel,
        int dynamicSharedMemorySizeInBytes,
        int maxGroupSize,
        out int minGridSize)
    {
        if (kernel is not DebugKernel)
            throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        // Estimation
        minGridSize = NumThreads;
        return 1;
    }

    #endregion

    #region Page Lock Scope

    /// <inheritdoc/>
    protected unsafe override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
        IntPtr pinned,
        long numElements) =>
        new CPUPageLockScope<T>(pinned, numElements);

    #endregion

    #region IDisposable

    /// <summary>
    /// Dispose all managed resources allocated by this CPU accelerator instance.
    /// </summary>
    protected override void DisposeAccelerator_Locked(bool disposing)
    {
        if (!disposing)
            return;

        // Dispose barriers
        _taskConcurrencyLimit.Dispose();
    }

    #endregion
}

#pragma warning restore CA1508 // Avoid dead conditional code
