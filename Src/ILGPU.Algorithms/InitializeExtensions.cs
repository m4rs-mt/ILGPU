// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: InitializeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Performs an initialization on the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The element view.</param>
    /// <param name="value">The target value.</param>
    public delegate void Initializer<T, TStride>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        T value)
        where T : unmanaged
        where TStride : struct, IStride1D;

    /// <summary>
    /// Initialize functionality for accelerators.
    /// </summary>
    public static class InitializeExtensions
    {
        #region Initialize Implementation

        /// <summary>
        /// A actual raw initializer implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        internal readonly struct InitializerImplementation<T, TStride> :
            IGridStrideKernelBody
            where T : unmanaged
            where TStride : struct, IStride1D
        {
            /// <summary>
            /// Creates a new initializer implementation.
            /// </summary>
            /// <param name="view">The parent target view.</param>
            /// <param name="value">The initializer value.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public InitializerImplementation(ArrayView1D<T, TStride> view, T value)
            {
                View = view;
                Value = value;
            }

            /// <summary>
            /// Returns the target view.
            /// </summary>
            public ArrayView1D<T, TStride> View { get; }

            /// <summary>
            /// Returns the initializer value.
            /// </summary>
            public T Value { get; }

            /// <summary>
            /// Executes this sequencer wrapper.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Execute(LongIndex1D linearIndex)
            {
                if (linearIndex >= View.Length)
                    return;

                View[linearIndex] = Value;
            }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Finish() { }
        }

        /// <summary>
        /// Creates a raw initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded initializer.</returns>
        private static Action<
            AcceleratorStream,
            LongIndex1D,
            InitializerImplementation<T, TStride>>
            CreateRawInitializer<T, TStride>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D =>
            accelerator.LoadGridStrideKernel<
                InitializerImplementation<T, TStride>>();

        /// <summary>
        /// Creates an initializer that is defined by the given element type.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        public static Initializer<T, TStride> CreateInitializer<T, TStride>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
        {
            var rawInitializer = accelerator.CreateRawInitializer<T, TStride>();
            return (stream, view, value) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));

                rawInitializer(
                    stream,
                    view.Length,
                    new InitializerImplementation<T, TStride>(
                        view,
                        value));
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
            where T : unmanaged =>
            accelerator.CreateInitializer<T, Stride1D.Dense>()(
                stream,
                view,
                value);

        /// <summary>
        /// Performs an initialization on the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The element view.</param>
        /// <param name="value">The target value.</param>
        public static void Initialize<T, TStride>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            T value)
            where T : unmanaged
            where TStride : struct, IStride1D =>
            accelerator.CreateInitializer<T, TStride>()(
                stream,
                view,
                value);

        #endregion
    }
}
