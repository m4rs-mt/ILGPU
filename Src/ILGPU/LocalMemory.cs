// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;

namespace ILGPU;

/// <summary>
/// Contains methods to allocate and manage local memory.
/// </summary>
public static partial class LocalMemory
{
    /// <summary>
    /// Allocates a single element in local memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>An allocated element in local memory.</returns>
    [LocalMemoryIntrinsic]
    public static ArrayView<T> Allocate<T>(int extent) where T : unmanaged =>
        throw new InvalidKernelOperationException();
}
