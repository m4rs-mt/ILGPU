// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SpinLock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Synchronization;

/// <summary>
/// Implements a busy-wait (spin) lock on a device.
/// </summary>
/// <remarks>
/// Use with caution since misuse can lead to deadlocks and massive performance
/// degradation.
/// </remarks>
public readonly ref struct SpinLock
{
    #region Instance

    private readonly ref int _address;

    /// <summary>
    /// Constructs a new spin lock.
    /// </summary>
    /// <param name="fieldAddress">
    /// The target field address in global memory to use.
    /// </param>
    public SpinLock(ref int fieldAddress)
    {
        _address = ref fieldAddress;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resets the internal state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Atomic.Exchange(ref _address, 0);

    /// <summary>
    /// Waits for a released lock.
    /// </summary>
    /// <remarks>
    /// Caution: ensure that the internal state is reset before calling the
    /// <see cref="Wait"/> method.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait()
    {
        while (Atomic.Exchange(ref _address, 1) == 1) ;
    }

    /// <summary>
    /// Releases the current lock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release() => Atomic.Exchange(ref _address, 0);

    #endregion
}

/// <summary>
/// Implements a busy-wait (spin) lock on a device across warps. Each first lane of every
/// warp in all groups tries to get access to the underling spin lock. If successful,
/// all threads in that warp continue processing.
/// </summary>
/// <remarks>
/// Use with caution since misuse can lead to deadlocks and massive performance
/// degradation.
/// </remarks>
public readonly ref struct WarpSpinLock
{
    #region Instance

    private readonly ref int _address;

    /// <summary>
    /// Constructs a new spin lock.
    /// </summary>
    /// <param name="fieldAddress">
    /// The target field address in global memory to use.
    /// </param>
    public WarpSpinLock(ref int fieldAddress)
    {
        _address = ref fieldAddress;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resets the internal state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        if (Warp.IsFirstLane)
            Atomic.Exchange(ref _address, 0);
    }

    /// <summary>
    /// Waits for a released lock.
    /// </summary>
    /// <remarks>
    /// Caution: ensure that the internal state is reset before calling the
    /// <see cref="Wait"/> method.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait()
    {
        if (Warp.IsFirstLane)
        {
            while (Atomic.Exchange(ref _address, 1) == 1) ;
        }
        Warp.Barrier();
    }

    /// <summary>
    /// Releases the current lock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release()
    {
        if (Warp.IsFirstLane)
            Atomic.Exchange(ref _address, 0);
    }

    #endregion
}
