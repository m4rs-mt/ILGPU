// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: SequenceExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Sequencers;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    #region Sequence Delegates

    /// <summary>
    /// Computes a new sequence of values from 0 to view.Length - 1 and writes
    /// the computed values to the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void Sequencer<T, TStride, TSequencer>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        TSequencer sequencer)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new sequence of batched values of length sequenceBatchLength, and
    /// writes the computed values to the given view. Afterwards, the target view will
    /// contain the following values:
    /// - [0, sequenceBatchLength - 1] = 0,,
    /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void BatchedSequencer<T, TStride, TSequencer>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        LongIndex1D sequenceBatchLength,
        TSequencer sequencer)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to
    /// sequenceLength, ... and writes the computed values to the given view. Afterwards,
    /// the target view will contain the following values:
    /// - [0, sequenceLength - 1] = [0, sequenceLength]
    /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void RepeatedSequencer<T, TStride, TSequencer>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        LongIndex1D sequenceLength,
        TSequencer sequencer)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new repeated sequence (of length sequenceLength) of batched values (of
    /// length sequenceBatchLength), and writes the computed values to the given view.
    /// Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] =
    ///       - [0, sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - [sequenceLength, sequenceLength * 2 - 1]
    ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceLength + sequenceBatchLength,
    ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void RepeatedBatchedSequencer<T, TStride, TSequencer>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        LongIndex1D sequenceLength,
        LongIndex1D sequenceBatchLength,
        TSequencer sequencer)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TSequencer : struct, ISequencer<T>;

    #endregion

    /// <summary>
    /// Sequencer functionality for accelerators.
    /// </summary>
    public static class SequenceExtensions
    {
        #region Sequence Implementation

        /// <summary>
        /// A actual raw sequencer implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        internal readonly struct SequenceImplementation<
            T,
            TStride,
            TSequencer> : IGridStrideKernelBody
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T>
        {
            /// <summary>
            /// Creates a new sequence implementation.
            /// </summary>
            /// <param name="sequenceLength">The length of the sequence.</param>
            /// <param name="sequenceBatchLength">
            /// The length of a single batch within a sequence.
            /// </param>
            /// <param name="sequencer">The sequencer instance.</param>
            /// <param name="view">The parent target view.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SequenceImplementation(
                LongIndex1D sequenceLength,
                LongIndex1D sequenceBatchLength,
                ArrayView1D<T, TStride> view,
                TSequencer sequencer)
            {
                SequenceLength = sequenceLength;
                SequenceBatchLength = sequenceBatchLength;
                View = view;
                Sequencer = sequencer;
            }

            /// <summary>
            /// Returns length of the sequence.
            /// </summary>
            public LongIndex1D SequenceLength { get; }

            /// <summary>
            /// The length of a single batch within a sequence.
            /// </summary>
            public LongIndex1D SequenceBatchLength { get; }

            /// <summary>
            /// Returns the target view.
            /// </summary>
            public ArrayView1D<T, TStride> View { get; }

            /// <summary>
            /// Returns the sequencer instance.
            /// </summary>
            public TSequencer Sequencer { get; }

            /// <summary>
            /// Executes this sequencer wrapper.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Execute(LongIndex1D linearIndex)
            {
                if (linearIndex >= View.Length)
                    return;

                long sequenceIndex = (linearIndex / SequenceBatchLength)
                    % SequenceLength;
                View[linearIndex] = Sequencer.ComputeSequenceElement(sequenceIndex);
            }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Finish() { }
        }

        /// <summary>
        /// Creates a raw sequencer that is defined by the given element type and the type
        /// of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        private static Action<
            AcceleratorStream,
            LongIndex1D,
            SequenceImplementation<T, TStride, TSequencer>>
            CreateRawSequencer<T, TStride, TSequencer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T> =>
            accelerator.LoadGridStrideKernel<
                SequenceImplementation<T, TStride, TSequencer>>();

        #endregion

        /// <summary>
        /// Creates a sequencer that is defined by the given element type and the type of
        /// the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static Sequencer<T, TStride, TSequencer> CreateSequencer<
            T,
            TStride,
            TSequencer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<
                T,
                TStride,
                TSequencer>();
            return (stream, view, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                rawSequencer(
                    stream,
                    view.Length,
                    new SequenceImplementation<T, TStride, TSequencer>(
                        view.Length,
                        1L,
                        view,
                        sequencer));
            };
        }

        /// <summary>
        /// Creates a batched sequencer that is defined by the given element type and the
        /// type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static BatchedSequencer<T, TStride, TSequencer> CreateBatchedSequencer<
            T,
            TStride,
            TSequencer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TStride, TSequencer>();
            return (stream, view, sequenceBatchLength, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (sequenceBatchLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceBatchLength));
                rawSequencer(
                    stream,
                    view.Length,
                    new SequenceImplementation<T, TStride, TSequencer>(
                        view.Length,
                        sequenceBatchLength,
                        view,
                        sequencer));
            };
        }

        /// <summary>
        /// Creates a repeated sequencer that is defined by the given element type and the
        /// type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static RepeatedSequencer<T, TStride, TSequencer> CreateRepeatedSequencer<
            T,
            TStride,
            TSequencer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TStride, TSequencer>();
            return (stream, view, sequenceLength, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (sequenceLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceLength));
                rawSequencer(
                    stream,
                    view.Length,
                    new SequenceImplementation<T, TStride, TSequencer>(
                        sequenceLength,
                        1L,
                        view,
                        sequencer));
            };
        }

        /// <summary>
        /// Creates a repeated batched sequencer that is defined by the given element type
        /// and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of all views.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static RepeatedBatchedSequencer<T, TStride, TSequencer>
            CreateRepeatedBatchedSequencer<T, TStride, TSequencer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TStride, TSequencer>();
            return (stream, view, sequenceLength, sequenceBatchLength, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (sequenceLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceLength));
                if (sequenceBatchLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceBatchLength));
                rawSequencer(
                    stream,
                    view.Length,
                    new SequenceImplementation<T, TStride, TSequencer>(
                        sequenceLength,
                        sequenceBatchLength,
                        view,
                        sequencer));
            };
        }

        /// <summary>
        /// Computes a new sequence of values from 0 to view.Length - 1 and writes
        /// the computed values to the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void Sequence<T, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> view,
            TSequencer sequencer)
            where T : unmanaged
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateSequencer<T, Stride1D.Dense, TSequencer>()(
                stream,
                view,
                sequencer);

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to
        /// sequenceLength, ... and writes the computed values to the given view.
        /// Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = [0, sequenceLength]
        /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedSequence<T, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> view,
            LongIndex1D sequenceLength,
            TSequencer sequencer)
            where T : unmanaged
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateRepeatedSequencer<T, Stride1D.Dense, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequencer);

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and
        /// writes the computed values to the given view. Afterwards, the target view will
        /// contain the following values:
        /// - [0, sequenceBatchLength - 1] = 0,,
        /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void BatchedSequence<T, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> view,
            LongIndex1D sequenceBatchLength,
            TSequencer sequencer)
            where T : unmanaged
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateBatchedSequencer<T, Stride1D.Dense, TSequencer>()(
                stream,
                view,
                sequenceBatchLength,
                sequencer);

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values
        /// (of length sequenceBatchLength), and writes the computed values to the given
        /// view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength,
        ///          sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength,
        ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedBatchedSequence<T, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> view,
            LongIndex1D sequenceLength,
            LongIndex1D sequenceBatchLength,
            TSequencer sequencer)
            where T : unmanaged
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateRepeatedBatchedSequencer<T, Stride1D.Dense, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);

        /// <summary>
        /// Computes a new sequence of values from 0 to view.Length - 1 and writes
        /// the computed values to the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void Sequence<T, TStride, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            TSequencer sequencer)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateSequencer<T, TStride, TSequencer>()(
                stream,
                view,
                sequencer);

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to
        /// sequenceLength, ... and writes the computed values to the given view.
        /// Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = [0, sequenceLength]
        /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedSequence<T, TStride, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            LongIndex1D sequenceLength,
            TSequencer sequencer)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateRepeatedSequencer<T, TStride, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequencer);

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and
        /// writes the computed values to the given view. Afterwards, the target view will
        /// contain the following values:
        /// - [0, sequenceBatchLength - 1] = 0,,
        /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void BatchedSequence<T, TStride, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            LongIndex1D sequenceBatchLength,
            TSequencer sequencer)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateBatchedSequencer<T, TStride, TSequencer>()(
                stream,
                view,
                sequenceBatchLength,
                sequencer);

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values
        /// (of length sequenceBatchLength), and writes the computed values to the given
        /// view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength,
        ///          sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength,
        ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the view.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedBatchedSequence<T, TStride, TSequencer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            LongIndex1D sequenceLength,
            LongIndex1D sequenceBatchLength,
            TSequencer sequencer)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TSequencer : struct, ISequencer<T> =>
            accelerator.CreateRepeatedBatchedSequencer<T, TStride, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);
    }
}
