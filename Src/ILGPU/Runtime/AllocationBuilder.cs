// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: AllocationBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime;

/// <summary>
/// Represents a simple and efficient builder for composed buffers.
/// </summary>
/// <param name="stream">The current accelerator stream.</param>
public partial class AllocationBuilder(AcceleratorStream stream)
{
    /// <summary>
    /// Returns the underlying stream.
    /// </summary>
    public AcceleratorStream Stream { get; } = stream;

    /// <summary>
    /// Returns the total length in bytes.
    /// </summary>
    public long LengthInBytes { get; private set; }

    /// <summary>
    /// Registers a buffer using the given element size and length.
    /// </summary>
    /// <param name="elementSize">The size of element.</param>
    /// <param name="length">The number of elements to allocate.</param>
    public void AddBuffer(int elementSize, long length)
    {
        if (elementSize < 1)
            throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));

        // Compensate for dynamic alignment requirements
        ++length;

        // Accumulate buffer size
        LengthInBytes += length * elementSize;
    }

    /// <summary>
    /// Registers a buffer with the given element type and length information.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    public void AddBuffer<T>(long length) where T : unmanaged =>
        AddBuffer(Interop.SizeOf<T>(), length);

    /// <summary>
    /// Converts this allocation builder into a real memory buffer wrapped into an
    /// allocation manager.
    /// </summary>
    /// <returns>The created allocation manager.</returns>
    public AllocationManager ToManager() => new(Stream, LengthInBytes);
}

partial class AcceleratorStream
{
    /// <summary>
    /// Creates a new allocation builder to be used with the current accelerator stream.
    /// </summary>
    /// <returns>The created allocation builder.</returns>
    public AllocationBuilder CreateAllocationBuilder() => new(this);
}
