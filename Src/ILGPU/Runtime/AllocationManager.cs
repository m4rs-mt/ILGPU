// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: AllocationManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable CA2213 // Disposable fields should be disposed
#pragma warning disable CA2201 // Do not raise reserved exception types

namespace ILGPU.Runtime;

/// <summary>
/// Implements a simple and greedy allocation manager to perform temporary memory
/// allocations on allocated accelerator buffers.
/// </summary>
public class AllocationManager : AcceleratorObject
{
    /// <summary>
    /// Represents a scope for a currently allocated buffer. Make sure to dispose the
    /// object to return allocated buffer data.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct AllocationScope<T> : IDisposable
        where T : unmanaged
    {
        private readonly BufferEntry _entry;

        internal AllocationScope(
            AllocationManager allocationManager,
            ArrayView<T> view,
            BufferEntry entry)
        {
            _entry = entry;
            AllocationManager = allocationManager;
            View = view;
        }

        /// <summary>
        /// Returns the parent allocation manager.
        /// </summary>
        public AllocationManager AllocationManager { get; }

        /// <summary>
        /// Returns the allocated array view.
        /// </summary>
        public ArrayView<T> View { get; }

        /// <summary>
        /// Frees the temporarily allocated chunk of memory.
        /// </summary>
        public void Dispose() => AllocationManager.Free(_entry);
    }

    /// <summary>
    /// An internal buffer entry to be used for a single allocation.
    /// </summary>
    /// <param name="Offset">The offset in bytes.</param>
    /// <param name="Size">The size in bytes.</param>
    internal readonly record struct BufferEntry(long Offset, long Size)
    {
        /// <summary>
        /// Returns the end offset of this entry.
        /// </summary>
        public long EndOffset => Offset + Size;
    }

    /// <summary>
    /// An internal size comparer for <see cref="BufferEntry"/> instances to be used
    /// for efficient sorting.
    /// </summary>
    sealed class SizeComparer : IComparer<BufferEntry>
    {
        public static readonly SizeComparer Instance = new();
        public int Compare(BufferEntry x, BufferEntry y) => x.Size.CompareTo(y.Size);
    }

    /// <summary>
    /// An internal offset comparer for <see cref="BufferEntry"/> instances to be used
    /// for efficient sorting.
    /// </summary>
    sealed class OffsetComparer : IComparer<BufferEntry>
    {
        public static readonly OffsetComparer Instance = new();
        public int Compare(BufferEntry x, BufferEntry y) => x.Offset.CompareTo(y.Offset);
    }

    private readonly object _lock = new();
    private readonly MemoryBuffer _buffer;
    private InlineList<BufferEntry> _entries = InlineList<BufferEntry>.Create(16);

    /// <summary>
    /// Constructs a new allocation manager.
    /// </summary>
    /// <param name="stream">The current accelerator stream.</param>
    /// <param name="lengthInBytes">The number of byes to allocate.</param>
    internal AllocationManager(AcceleratorStream stream, long lengthInBytes)
        : base(stream.Accelerator)
    {
        _buffer = stream.Allocate1D<byte>(lengthInBytes);
        _entries.Add(new BufferEntry(0, lengthInBytes));
    }

    /// <summary>
    /// Allocates a number of elements of a specific type.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements.</param>
    /// <returns>An allocated view in the case of a successful allocation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public AllocationScope<T>? Allocate<T>(long length) where T : unmanaged
    {
        // Check for empty allocations
        if (length < 1L) return new(this, new(_buffer, 0L, 0L), default);

        // Compute total size
        int elementSize = Interop.SizeOf<T>();
        long totalSize = (length + 1) * elementSize;

        lock (_lock)
        {
            // Search for best buffer entry
            int index = _entries.AsSpan().BinarySearch(
                new(0, totalSize),
                SizeComparer.Instance);
            if (index < 0) index = ~index;

            // Check whether we have a free spot
            if (index >= _entries.Count) return null;

            var matchedEntry = _entries[index];
            _entries.RemoveAt(index);

            // Check for splitting of blocks
            if (matchedEntry.Size > totalSize)
            {
                // Split the block and keep the remainder in the list
                var remainder = new BufferEntry(
                    matchedEntry.Offset + totalSize,
                    matchedEntry.Size - totalSize);
                _entries.Insert(remainder, index);
            }

            var entry = new BufferEntry(matchedEntry.Offset, totalSize);
            var view = _buffer
                .AsArrayView<byte>(entry.Offset, entry.Size)
                .AlignTo(elementSize)
                .Main.Cast<T>();
            return new(this, view, entry);
        }
    }

    /// <summary>
    /// Frees the given buffer entry.
    /// </summary>
    /// <param name="entry">The buffer entry to free.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Free(BufferEntry entry)
    {
        if (entry.Size < 1L) return;

        lock (_lock)
        {
            // Find new insert position
            int insertIndex = _entries.AsSpan().BinarySearch(
                entry,
                OffsetComparer.Instance);
            if (insertIndex < 0) insertIndex = ~insertIndex;
            _entries.Insert(entry, insertIndex);

            // Merge all entries greedily
            for (int i = 0; i < _entries.Count - 1; ++i)
            {
                var current = _entries[i];
                var next = _entries[i + 1];

                if (current.EndOffset != next.Offset)
                    continue;

                _entries[i] = new(current.Offset, current.Size + next.Size);
                _entries.RemoveAt(i + 1);
                --i;
            }
        }
    }

    /// <summary>
    /// Disposes the underlying buffer.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing) =>
        _buffer.Dispose();
}

partial class AcceleratorStream
{
    /// <summary>
    /// A buffer allocation builder for temporary demands.
    /// </summary>
    public sealed class TemporaryBuffersBuilder : AllocationBuilder, IDisposable
    {
        private readonly AcceleratorStream _stream;
        private bool _disposedValue;

        internal TemporaryBuffersBuilder(AcceleratorStream stream) : base(stream)
        {
            _stream = stream;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                    _stream.FinishTemporaryAllocationBuffers(this);

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes this builder while configuring temporary buffer resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    private AllocationManager? _defaultManager;

    /// <summary>
    /// Configures temporary buffer allocations.
    /// </summary>
    /// <returns>The allocation buffers builder.</returns>
    public TemporaryBuffersBuilder ConfigureTemporaryBuffers() => new(this);

    /// <summary>
    /// Finishes a building process of a temporary buffer.
    /// </summary>
    /// <param name="builder">The builder to use.</param>
    private void FinishTemporaryAllocationBuffers(TemporaryBuffersBuilder builder) =>
        _defaultManager = builder.ToManager();

    /// <summary>
    /// Tries to allocate the given number of elements of the specified type.
    /// </summary>
    /// <typeparam name="T">The element type to allocate in memory.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>The allocation scope in case of a successful allocation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Will be thrown if temporary buffers are not configured.
    /// </exception>
    public AllocationManager.AllocationScope<T>? TryAllocateTemporary<T>(long length)
        where T : unmanaged
    {
        if (_defaultManager is null)
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.NotSupportedTemporaryAllocation);
        }

        return _defaultManager.Allocate<T>(length);
    }

    /// <summary>
    /// Allocate the given number of elements of the specified type.
    /// </summary>
    /// <typeparam name="T">The element type to allocate in memory.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>The allocation scope in case of a successful allocation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Will be thrown if temporary buffers are not configured.
    /// </exception>
    /// <exception cref="OutOfMemoryException">
    /// Will be thrown if memory could not be allocated.
    /// </exception>
    public AllocationManager.AllocationScope<T> AllocateTemporary<T>(long length)
        where T : unmanaged =>
        TryAllocateTemporary<T>(length) ?? throw new OutOfMemoryException();
}

#pragma warning restore CA2213 // Disposable fields should be disposed
#pragma warning restore CA2201 // Do not raise reserved exception types
