// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SynchronizationExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Synchronization;
using SpinLock = ILGPU.Synchronization.SpinLock;

namespace ILGPU;

partial class Grid
{
    /// <summary>
    /// Returns a new sequential group executor for the current grid.
    /// </summary>
    /// <returns>The new sequential group executor.</returns>
    public static SequentialGroupExecutor GetSequentialGroupExecutor()
    {
        ref int globalTemporary = ref GetInitializedTemporaryValue<int>();
        return new(ref globalTemporary);
    }

    /// <summary>
    /// Returns a new sequential group executor for the current grid.
    /// </summary>
    /// <returns>The new sequential group executor.</returns>
    public static SequentialGroupExecutor<T> GetSequentialGroupExecutor<T>()
        where T : unmanaged
    {
        ref int globalTemporary = ref GetInitializedTemporaryValue<int>();
        ref T globalValueTemporary = ref GetInitializedTemporaryValue<T>();
        return new(ref globalTemporary, ref globalValueTemporary);
    }

    /// <summary>
    /// Returns a new spin lock for the current grid.
    /// </summary>
    /// <returns>The new spin lock.</returns>
    public static SpinLock GetSpinLock()
    {
        ref int globalTemporary = ref GetInitializedTemporaryValue<int>();
        return new(ref globalTemporary);
    }
}

partial class Group
{
    /// <summary>
    /// Returns a new spin lock for the current group.
    /// </summary>
    /// <returns>The new spin lock.</returns>
    public static WarpSpinLock GetWarpSpinLock()
    {
        var globalTemporary = GetInitializedTemporaryValue<int>();
        return new(ref globalTemporary.Value);
    }
}
