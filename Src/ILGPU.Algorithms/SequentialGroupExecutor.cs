// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: SequentialGroupExecutor.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Realizes a seqential group-execution pattern via a device-wide barrier.
    /// </summary>
    public readonly struct SequentialGroupExecutor
    {
        #region Instance

        private readonly VariableView<int> address;

        /// <summary>
        /// Constructs a new sequential group executor.
        /// </summary>
        /// <param name="fieldAddress">The target field address in global memory to use.</param>
        public SequentialGroupExecutor(VariableView<int> fieldAddress)
        {
            address = fieldAddress;
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
                Atomic.Exchange(ref address.Value, 0);
        }

        /// <summary>
        /// Waits for all previous groups to finish.
        /// </summary>
        /// <remarks>
        /// Caution: ensure that the internal state is reset before calling the <see cref="Wait"/> method.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Wait()
        {
            if (Group.IsFirstThread)
            {
                do { }
                while (Atomic.CompareExchange(ref address.Value, int.MaxValue, 0) < Grid.IdxX);
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
                Atomic.Add(ref address.Value, 1);
        }

        #endregion
    }

    /// <summary>
    /// Realizes a sequential group-execution pattern via a device-wide barrier
    /// that can pass an element of type <typeparamref name="T"/> to another group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct SequentialGroupExecutor<T>
        where T : unmanaged
    {
        #region Instance

        private readonly VariableView<T> address;
        private readonly SequentialGroupExecutor executor;

        /// <summary>
        /// Constructs a new sequential group executor.
        /// </summary>
        /// <param name="executorView">The target field address in global memory to use.</param>
        /// <param name="dataView">The target data address in global memory to use.</param>
        public SequentialGroupExecutor(VariableView<int> executorView, VariableView<T> dataView)
        {
            address = dataView;
            executor = new SequentialGroupExecutor(executorView);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Resets the internal state.
        /// </summary>
        public void Reset() => executor.Reset();

        /// <summary>
        /// Waits for all previous groups to finish.
        /// </summary>
        /// <returns>The value from the previous group.</returns>
        /// <remarks>
        /// Caution: ensure that the internal state is reset before calling the <see cref="Wait"/> method.
        /// </remarks>
        public T Wait()
        {
            executor.Wait();
            return address.Value;
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
                address.Value = value;
                MemoryFence.DeviceLevel();
            }
            executor.Release();
        }

        #endregion
    }
}
