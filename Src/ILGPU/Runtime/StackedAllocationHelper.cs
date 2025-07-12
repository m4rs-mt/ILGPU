// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: StackedAllocationHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime;

/// <summary>
/// Simplifies the subsequent splitting of a temporary memory view
/// into smaller chunks.
/// </summary>
public record struct StackedAllocationHelper(ArrayView<byte> DataView)
{
    #region Properties

    /// <summary>
    /// Returns the total number of allocated bytes.
    /// </summary>
    public long Allocated { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Allocates several elements of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type to allocate.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>The allocated array view.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArrayView<T> Allocate<T>(long length) where T : unmanaged
    {
        // Ensure correct alignment
        var sizeOfAllocateT = Interop.SizeOf<T>();
        var alignedBoundary = Interop.Align(Allocated, sizeOfAllocateT);

        // Compute global offset and sub view length
        long newOffset = Allocated + alignedBoundary;
        long subLength = length * sizeOfAllocateT;
        Trace.Assert(newOffset + subLength <= DataView.Length);

        // Determine view and update internal allocation information
        var tempView = DataView.SubView(newOffset, subLength);
        Allocated += newOffset + subLength;
        return tempView.Cast<T>();
    }

    #endregion
}
