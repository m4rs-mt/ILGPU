// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: InitializeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Performs an initialization on the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The element view.</param>
    /// <param name="value">The target value.</param>
    public delegate void Initializer<T>(
        AcceleratorStream stream,
        ArrayView<T> view,
        T value)
        where T : unmanaged;

    /// <summary>
    /// Initialize functionality for accelerators.
    /// </summary>
    public static class InitializeExtensions
    {
        #region Initialize Implementation

        /// <summary>
        /// The actual initialize implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="index">The current thread index.</param>
        /// <param name="view">The target view.</param>
        /// <param name="value">The value.</param>
        internal static void InitializeKernel<T>(Index1 index, ArrayView<T> view, T value)
            where T : unmanaged
        {
            var stride = GridExtensions.GridStrideLoopStride;
            for (var idx = index; idx < view.Length; idx += stride)
                view[idx] = value;
        }

        /// <summary>
        /// Creates a raw initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="minDataSize">The minimum data size for maximum occupancy.</param>
        /// <returns>The loaded initializer.</returns>
        private static Action<AcceleratorStream, Index1, ArrayView<T>, T> CreateRawInitializer<T>(
            this Accelerator accelerator,
            out Index1 minDataSize)
            where T : unmanaged
        {
            var result = accelerator.LoadAutoGroupedKernel(
                (Action<Index1, ArrayView<T>, T>)InitializeKernel,
                out int groupSize,
                out int minGridSize);
            minDataSize = groupSize * minGridSize;
            return result;
        }

        /// <summary>
        /// Creates an initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        public static Initializer<T> CreateInitializer<T>(
            this Accelerator accelerator)
            where T : unmanaged
        {
            var rawInitializer = accelerator.CreateRawInitializer<T>(out Index1 minDataSize);
            return (stream, view, value) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                rawInitializer(stream, Math.Min(view.Length, minDataSize), view, value);
            };
        }

        /// <summary>
        /// Performs an initialization on the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        public static void Initialize<T>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> view,
            T value)
            where T : unmanaged
        {
            accelerator.CreateInitializer<T>()(
                stream,
                view,
                value);
        }

        #endregion
    }
}
