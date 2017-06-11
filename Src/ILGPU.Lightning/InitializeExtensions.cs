// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: InitializeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Reflection;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents the implementation of the initialize functionality.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    static class InitializeImpl<T>
        where T : struct
    {
        /// <summary>
        /// Represents an initialize kernel.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(InitializeImpl<T>).GetMethod(
                nameof(Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void Kernel(
            Index index,
            ArrayView<T> view,
            T value)
        {
            var stride = GridExtensions.GridStrideLoopStride;
            for (var idx = index; idx < view.Length; idx += stride)
                view[idx] = value;
        }
    }

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
        where T : struct;

    /// <summary>
    /// Initialize functionality for lightning contexts.
    /// </summary>
    public static class InitializeExtensions
    {
        #region Initialize

        /// <summary>
        /// Creates a raw initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="minDataSize">The minimum data size for maximum occupancy.</param>
        /// <returns>The loaded initializer.</returns>
        private static Action<AcceleratorStream, Index, ArrayView<T>, T> CreateRawInitializer<T>(
            this LightningContext context,
            out Index minDataSize)
            where T : struct
        {
            var result = context.LoadAutoGroupedKernel<Action<AcceleratorStream, Index, ArrayView<T>, T>>(
                InitializeImpl<T>.KernelMethod, out int groupSize, out int minGridSize);
            minDataSize = groupSize * minGridSize;
            return result;
        }

        /// <summary>
        /// Creates an initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <returns>The loaded transformer.</returns>
        public static Initializer<T> CreateInitializer<T>(
            this LightningContext context)
            where T : struct
        {
            var rawInitializer = context.CreateRawInitializer<T>(out Index minDataSize);
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
        /// <param name="context">The lightning context.</param>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        public static void Initialize<T>(
            this LightningContext context,
            ArrayView<T> view,
            T value)
            where T : struct
        {
            context.Initialize(context.DefaultStream, view, value);
        }

        /// <summary>
        /// Performs an initialization on the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        public static void Initialize<T>(
            this LightningContext context,
            AcceleratorStream stream,
            ArrayView<T> view,
            T value)
            where T : struct
        {
            context.CreateInitializer<T>()(
                stream,
                view,
                value);
        }

        #endregion
    }
}
