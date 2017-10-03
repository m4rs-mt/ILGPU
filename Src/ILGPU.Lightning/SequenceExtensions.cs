// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: SequenceExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents an abstract interface for a sequencer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    public interface ISequencer<T>
        where T : struct
    {
        /// <summary>
        /// Computes the sequence element for the corresponding <paramref name="sequenceIndex"/>.
        /// </summary>
        /// <param name="sequenceIndex">The sequence index for the computation of the corresponding value.</param>
        /// <returns>The computed sequence value.</returns>
        T ComputeSequenceElement(Index sequenceIndex);
    }

    /// <summary>
    /// Implements a sequence algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    static class SequenceImpl<T, TSequencer>
        where T : struct
        where TSequencer : struct, ISequencer<T>
    {
        /// <summary>
        /// Represents a reduction kernel.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(SequenceImpl<T, TSequencer>).GetMethod(
                nameof(Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void Kernel(
            Index index,
            ArrayView<T> view,
            Index sequenceLength,
            Index sequenceBatchLength,
            TSequencer sequencer)
        {
            var stride = GridExtensions.GridStrideLoopStride;
            for (var idx = index; idx < view.Length; idx += stride)
            {
                var sequenceIndex = (idx.X / sequenceBatchLength) % sequenceLength;
                view[idx] = sequencer.ComputeSequenceElement(sequenceIndex);
            }
        }
    }

    #region Sequence Delegates

    /// <summary>
    /// Computes a new sequence of values from 0 to view.Length - 1 and writes
    /// the computed values to the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void Sequencer<T, TSequencer>(
        AcceleratorStream stream,
        ArrayView<T> view,
        TSequencer sequencer)
        where T : struct
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new sequence of batched values of length sequenceBatchLength, and writes
    /// the computed values to the given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceBatchLength - 1] = 0,,
    /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void BatchedSequencer<T, TSequencer>(
        AcceleratorStream stream,
        ArrayView<T> view,
        Index sequenceBatchLength,
        TSequencer sequencer)
        where T : struct
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to sequenceLength, ... and writes
    /// the computed values to the given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] = [0, sequenceLength]
    /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void RepeatedSequencer<T, TSequencer>(
        AcceleratorStream stream,
        ArrayView<T> view,
        Index sequenceLength,
        TSequencer sequencer)
        where T : struct
        where TSequencer : struct, ISequencer<T>;

    /// <summary>
    /// Computes a new repeated sequence (of length sequenceLength) of batched values (of length sequenceBatchLength),
    /// and writes the computed values to the given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] =
    ///       - [0, sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - [sequenceLength, sequenceLength * 2 - 1]
    ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The target view.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequencer">The used sequencer.</param>
    public delegate void RepeatedBatchedSequencer<T, TSequencer>(
        AcceleratorStream stream,
        ArrayView<T> view,
        Index sequenceLength,
        Index sequenceBatchLength,
        TSequencer sequencer)
        where T : struct
        where TSequencer : struct, ISequencer<T>;

    #endregion

    /// <summary>
    /// Sequencer functionality for accelerators.
    /// </summary>
    public static class SequenceExtensions
    {
        /// <summary>
        /// Creates a raw sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="minDataSize">The minimum data size for maximum occupancy.</param>
        /// <returns>The loaded sequencer.</returns>
        private static Action<AcceleratorStream, Index, ArrayView<T>, Index, Index, TSequencer> CreateRawSequencer<T, TSequencer>(
            this Accelerator accelerator,
            out Index minDataSize)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            var result = accelerator.LoadAutoGroupedKernel<Action<AcceleratorStream, Index, ArrayView<T>, Index, Index, TSequencer>>(
                SequenceImpl<T, TSequencer>.KernelMethod, out int groupSize, out int minGridSize);
            minDataSize = groupSize * minGridSize;
            return result;
        }

        /// <summary>
        /// Creates a sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static Sequencer<T, TSequencer> CreateSequencer<T, TSequencer>(
            this Accelerator accelerator)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TSequencer>(out Index minDataSize);
            return (stream, view, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                rawSequencer(stream, Math.Min(view.Length, minDataSize), view, view.Length, 1, sequencer);
            };
        }

        /// <summary>
        /// Creates a batched sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static BatchedSequencer<T, TSequencer> CreateBatchedSequencer<T, TSequencer>(
            this Accelerator accelerator)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TSequencer>(out Index minDataSize);
            return (stream, view, sequenceLength, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (sequenceLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceLength));
                rawSequencer(stream, view.Length, view, sequenceLength, 1, sequencer);
            };
        }

        /// <summary>
        /// Creates a repeated sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static BatchedSequencer<T, TSequencer> CreateRepeatedSequencer<T, TSequencer>(
            this Accelerator accelerator)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TSequencer>(out Index minDataSize);
            return (stream, view, sequenceBatchLength, sequencer) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (sequenceBatchLength < 1)
                    throw new ArgumentOutOfRangeException(nameof(sequenceBatchLength));
                rawSequencer(stream, view.Length, view, view.Length, sequenceBatchLength, sequencer);
            };
        }


        /// <summary>
        /// Creates a repeated batched sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded sequencer.</returns>
        public static RepeatedBatchedSequencer<T, TSequencer> CreateRepeatedBatchedSequencer<T, TSequencer>(
            this Accelerator accelerator)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            var rawSequencer = accelerator.CreateRawSequencer<T, TSequencer>(out Index minDataSize);
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
                rawSequencer(stream, view.Length, view, sequenceLength, sequenceBatchLength, sequencer);
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
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.CreateSequencer<T, TSequencer>()(
                stream,
                view,
                sequencer);
        }

        /// <summary>
        /// Computes a new sequence of values from 0 to view.Length - 1 and writes
        /// the computed values to the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void Sequence<T, TSequencer>(
            this Accelerator accelerator,
            ArrayView<T> view,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.Sequence(accelerator.DefaultStream, view, sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to sequenceLength, ... and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
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
            Index sequenceLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.CreateRepeatedSequencer<T, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to sequenceLength, ... and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = [0, sequenceLength]
        /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedSequence<T, TSequencer>(
            this Accelerator accelerator,
            ArrayView<T> view,
            Index sequenceLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.RepeatedSequence(
                accelerator.DefaultStream,
                view,
                sequenceLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
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
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.CreateBatchedSequencer<T, TSequencer>()(
                stream,
                view,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceBatchLength - 1] = 0,,
        /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void BatchedSequence<T, TSequencer>(
            this Accelerator accelerator,
            ArrayView<T> view,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.BatchedSequence(
                accelerator.DefaultStream,
                view,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values (of length sequenceBatchLength),
        /// and writes the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
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
            Index sequenceLength,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.CreateRepeatedBatchedSequencer<T, TSequencer>()(
                stream,
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values (of length sequenceBatchLength),
        /// and writes the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        public static void RepeatedBatchedSequence<T, TSequencer>(
            this Accelerator accelerator,
            ArrayView<T> view,
            Index sequenceLength,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            accelerator.RepeatedBatchedSequence(
                accelerator.DefaultStream,
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);
        }
    }

    namespace Sequencers
    {
        /// <summary>
        /// Represents an identity implementation of an index sequencer.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct IndexSequencer : ISequencer<Index>
        {
            /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
            public Index ComputeSequenceElement(Index sequenceIndex)
            {
                return sequenceIndex;
            }
        }

        /// <summary>
        /// Represents an identity implementation of a float sequencer.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct FloatSequencer : ISequencer<float>
        {
            /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
            public float ComputeSequenceElement(Index sequenceIndex)
            {
                return sequenceIndex;
            }
        }

        /// <summary>
        /// Represents an identity implementation of a double sequencer.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct DoubleSequencer : ISequencer<double>
        {
            /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
            public double ComputeSequenceElement(Index sequenceIndex)
            {
                return sequenceIndex;
            }
        }
    }
}
