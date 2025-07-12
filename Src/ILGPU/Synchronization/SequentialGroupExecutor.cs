// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SequentialGroupExecutor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Synchronization;

/// <summary>
/// Realizes a sequential group-execution pattern via a device-wide barrier.
/// </summary>
public readonly ref struct SequentialGroupExecutor
{
    #region Instance

    private readonly ref int _address;

    /// <summary>
    /// Constructs a new sequential group executor.
    /// </summary>
    /// <param name="fieldAddress">
    /// The target field address in global memory to use.
    /// </param>
    public SequentialGroupExecutor(ref int fieldAddress)
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
        if (Group.IsFirstThread)
            Atomic.Exchange(ref _address, 0);
    }

    /// <summary>
    /// Waits for all previous groups to finish.
    /// </summary>
    /// <remarks>
    /// Caution: ensure that the internal state is reset before calling the
    /// <see cref="Wait"/> method.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait()
    {
        if (Group.IsFirstThread)
        {
            do { }
            while (Atomic.CompareExchange(ref _address, int.MaxValue, 0) < Grid.Index);
        }
        Group.Barrier();
    }

    /// <summary>
    /// Signals the next group to continue processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release()
    {
        if (Group.IsFirstThread)
            Atomic.Add(ref _address, 1);
    }

    #endregion
}

/// <summary>
/// Realizes a sequential group-execution pattern via a device-wide barrier
/// that can pass an element of type <typeparamref name="T"/> to another group.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly ref struct SequentialGroupExecutor<T>
    where T : unmanaged
{
    #region Instance

    private readonly ref T _address;
    private readonly SequentialGroupExecutor _executor;

    /// <summary>
    /// Constructs a new sequential group executor.
    /// </summary>
    /// <param name="executorView">
    /// The target field address in global memory to use.
    /// </param>
    /// <param name="dataView">
    /// The target data address in global memory to use.
    /// </param>
    public SequentialGroupExecutor(
        ref int executorView,
        ref T dataView)
    {
        _address = ref dataView;
        _executor = new SequentialGroupExecutor(ref executorView);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resets the internal state.
    /// </summary>
    public void Reset() => _executor.Reset();

    /// <summary>
    /// Waits for all previous groups to finish.
    /// </summary>
    /// <returns>The value from the previous group.</returns>
    /// <remarks>
    /// Caution: ensure that the internal state is reset before calling the
    /// <see cref="Wait"/> method.
    /// </remarks>
    public T? Wait()
    {
        _executor.Wait();
        return Grid.Index > 0 ? _address : null;
    }

    /// <summary>
    /// Signals the next group to continue processing.
    /// </summary>
    /// <param name="value">The value that should be passed to the next group.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(T value)
    {
        if (Group.IsFirstThread)
        {
            _address = value;
            Grid.MemoryFence();
        }
        Group.Barrier();
        _executor.Release();
    }

    #endregion
}
