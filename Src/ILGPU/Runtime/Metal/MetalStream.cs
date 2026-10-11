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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
    /// MTLCommandBufferStatusCompleted.
    /// </summary>
    private const ulong StatusCompleted = 4;

    /// <summary>
    /// MTLCommandBufferStatusError.
    /// </summary>
    private const ulong StatusError = 5;

    /// <summary>
    /// The committed command buffers in commit order, tracked for synchronization
    /// until they are known to have completed without an error.
    /// </summary>
    private readonly List<IntPtr> _commandBuffers = [];

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
            // Release the leading command buffers that completed successfully;
            // failed ones stay tracked until a synchronization reports them.
            int completed = 0;
            while (completed < _commandBuffers.Count &&
                MetalAPI.GetCommandBufferStatus(_commandBuffers[completed]) ==
                    StatusCompleted)
            {
                MetalAPI.Release(_commandBuffers[completed++]);
            }
            _commandBuffers.RemoveRange(0, completed);

            // Retain and track the new one before committing.
            MetalAPI.Retain(commandBuffer);
            _commandBuffers.Add(commandBuffer);
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

        string? error = WaitForCommandBuffers();
        ThrowIfFailed(error ?? GetCommandBufferError(commandBuffer));
    }

    /// <inheritdoc/>
    /// <exception cref="MetalException">
    /// A command buffer committed since the last synchronization failed.
    /// </exception>
    public override void Synchronize() => ThrowIfFailed(WaitForCommandBuffers());

    /// <summary>
    /// Waits for all tracked command buffers to complete and releases them.
    /// </summary>
    /// <returns>
    /// The error of the first command buffer that failed, or null.
    /// </returns>
    private string? WaitForCommandBuffers()
    {
        lock (_syncLock)
        {
            string? error = null;
            foreach (var commandBuffer in _commandBuffers)
            {
                MetalAPI.WaitUntilCompleted(commandBuffer);
                error ??= GetCommandBufferError(commandBuffer);
                MetalAPI.Release(commandBuffer);
            }
            _commandBuffers.Clear();
            return error;
        }
    }

    /// <summary>
    /// Returns the error of a completed command buffer that failed.
    /// </summary>
    /// <param name="commandBuffer">The completed command buffer.</param>
    /// <returns>The error message, or null if the command buffer succeeded.</returns>
    private static string? GetCommandBufferError(IntPtr commandBuffer)
    {
        if (MetalAPI.GetCommandBufferStatus(commandBuffer) != StatusError)
            return null;
        var nsError = MetalAPI.GetCommandBufferError(commandBuffer);
        var code = MetalAPI.GetNSErrorCode(nsError).ToString(
            CultureInfo.InvariantCulture);
        var description = MetalAPI.GetNSErrorDescription(nsError) ?? "unknown error";
        return $"Metal command buffer failed (error {code}): {description}";
    }

    /// <summary>
    /// Throws a <see cref="MetalException"/> for a failed command buffer.
    /// </summary>
    /// <param name="error">The error message, or null if nothing failed.</param>
    private static void ThrowIfFailed(string? error)
    {
        if (error is not null)
            throw new MetalException(MetalError.CommandBufferFailed, error);
    }

    /// <inheritdoc/>
    protected override ProfilingMarker AddProfilingMarkerInternal() =>
        new MetalProfilingMarker(Accelerator!);

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        // Failed command buffers are reported by Synchronize, not when disposing
        WaitForCommandBuffers();

        if (_responsibleForHandle && CommandQueue != IntPtr.Zero)
        {
            MetalAPI.Release(CommandQueue);
            CommandQueue = IntPtr.Zero;
        }
    }
}
