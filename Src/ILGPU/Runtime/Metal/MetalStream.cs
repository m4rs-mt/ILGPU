// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a Metal command queue stream for compute dispatch.
/// Each stream owns a command queue. Command buffers are created transiently
/// per operation and tracked for synchronization.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Matches base class AcceleratorStream naming convention.")]
public sealed class MetalStream : AcceleratorStream
{
    /// <summary>
    /// Whether this stream is responsible for releasing the command queue.
    /// </summary>
    private readonly bool _responsibleForHandle;

    /// <summary>
    /// The last committed command buffer, tracked for synchronization.
    /// </summary>
    private IntPtr _lastCommandBuffer;

    /// <summary>
    /// Lock for synchronization operations.
    /// </summary>
    private readonly object _syncLock = new();

    /// <summary>
    /// Creates a new Metal stream from an existing command queue.
    /// </summary>
    /// <param name="accelerator">The parent accelerator.</param>
    /// <param name="commandQueue">The command queue handle.</param>
    /// <param name="responsible">
    /// Whether this stream owns the command queue handle.
    /// </param>
    internal MetalStream(
        Accelerator accelerator,
        IntPtr commandQueue,
        bool responsible)
        : base(accelerator, AcceleratorStreamFlags.None)
    {
        CommandQueue = commandQueue;
        _responsibleForHandle = responsible;
    }

    /// <summary>
    /// Creates a new Metal stream by creating a new command queue.
    /// </summary>
    /// <param name="accelerator">The parent Metal accelerator.</param>
    /// <param name="flags">Stream creation flags.</param>
    internal MetalStream(
        MetalAccelerator accelerator,
        AcceleratorStreamFlags flags)
        : base(accelerator, flags)
    {
        CommandQueue = MetalAPI.NewCommandQueue(accelerator.DeviceHandle);
        MetalException.ThrowIfNull(
            CommandQueue,
            IntPtr.Zero,
            MetalError.CommandQueueCreationFailed,
            "Failed to create Metal command queue for stream");
        _responsibleForHandle = true;
    }

    /// <summary>
    /// Returns the Metal command queue handle.
    /// </summary>
    internal IntPtr CommandQueue { get; private set; }

    /// <summary>
    /// Creates a new command buffer from this stream's command queue.
    /// </summary>
    /// <returns>Handle to the new command buffer.</returns>
    public IntPtr CreateCommandBuffer() =>
        MetalAPI.NewCommandBuffer(CommandQueue);

    /// <summary>
    /// Commits a command buffer for execution and tracks it for synchronization.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to commit.</param>
    public void CommitCommandBuffer(IntPtr commandBuffer)
    {
        lock (_syncLock)
        {
            // Release the previous tracked command buffer.
            if (_lastCommandBuffer != IntPtr.Zero)
                MetalAPI.Release(_lastCommandBuffer);

            // Retain and track the new one before committing.
            MetalAPI.Retain(commandBuffer);
            _lastCommandBuffer = commandBuffer;
        }

        MetalAPI.Commit(commandBuffer);
    }

    /// <summary>
    /// Commits a command buffer and waits for it to complete (synchronous).
    /// </summary>
    /// <param name="commandBuffer">The command buffer to commit and wait on.</param>
    internal void CommitAndWait(IntPtr commandBuffer)
    {
        MetalAPI.Commit(commandBuffer);
        MetalAPI.WaitUntilCompleted(commandBuffer);

        lock (_syncLock)
        {
            // The last tracked buffer is no longer needed since we just
            // synchronized past it.
            if (_lastCommandBuffer != IntPtr.Zero)
            {
                MetalAPI.Release(_lastCommandBuffer);
                _lastCommandBuffer = IntPtr.Zero;
            }
        }
    }

    /// <inheritdoc/>
    public override void Synchronize()
    {
        lock (_syncLock)
        {
            if (_lastCommandBuffer != IntPtr.Zero)
            {
                MetalAPI.WaitUntilCompleted(_lastCommandBuffer);
                MetalAPI.Release(_lastCommandBuffer);
                _lastCommandBuffer = IntPtr.Zero;
            }
        }
    }

    /// <inheritdoc/>
    protected override ProfilingMarker AddProfilingMarkerInternal() =>
        new MetalProfilingMarker(Accelerator!);

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        Synchronize();

        if (_responsibleForHandle && CommandQueue != IntPtr.Zero)
        {
            MetalAPI.Release(CommandQueue);
            CommandQueue = IntPtr.Zero;
        }
    }
}
