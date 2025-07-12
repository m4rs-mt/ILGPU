// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: RadixSortExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.RadixSort;

namespace ILGPU;

partial class Group
{
    /// <summary>
    /// Performs a group-wide radix sort pass.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">The radix sort operation.</typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value in the current lane.</returns>
    [GroupIntrinsic]
    public static T RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        throw new InvalidKernelOperationException();
}

partial class Warp
{
    /// <summary>
    /// Performs a warp-wide radix sort operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The radix sort operation type.
    /// </typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value for the current lane.</returns>
    [WarpIntrinsic]
    public static T RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        throw new InvalidKernelOperationException();
}
