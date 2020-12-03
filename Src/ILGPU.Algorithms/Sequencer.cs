// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: Sequencer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms.Sequencers
{
    /// <summary>
    /// Represents an abstract interface for a sequencer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    public interface ISequencer<T>
        where T : struct
    {
        /// <summary>
        /// Computes the sequence element for the corresponding
        /// <paramref name="sequenceIndex"/>.
        /// </summary>
        /// <param name="sequenceIndex">
        /// The sequence index for the computation of the corresponding value.
        /// </param>
        /// <returns>The computed sequence value.</returns>
        T ComputeSequenceElement(Index1 sequenceIndex);
    }

    /// <summary>
    /// Represents an identity implementation of an index sequencer.
    /// </summary>
    public readonly struct IndexSequencer : ISequencer<Index1>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index1)" />
        public readonly Index1 ComputeSequenceElement(Index1 sequenceIndex) =>
            sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a half sequencer.
    /// </summary>
    public readonly struct HalfSequencer : ISequencer<Half>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index1)" />
        public readonly Half ComputeSequenceElement(Index1 sequenceIndex) =>
            (Half)sequenceIndex.X;
    }

    /// <summary>
    /// Represents an identity implementation of a float sequencer.
    /// </summary>
    public readonly struct FloatSequencer : ISequencer<float>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index1)" />
        public readonly float ComputeSequenceElement(Index1 sequenceIndex) =>
            sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a double sequencer.
    /// </summary>
    public readonly struct DoubleSequencer : ISequencer<double>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index1)" />
        public readonly double ComputeSequenceElement(Index1 sequenceIndex) =>
            sequenceIndex;
    }

    /// <summary>
    /// Represents a sequencer that wraps an array view in a sequencer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct ViewSourceSequencer<T> : ISequencer<T>
        where T : unmanaged
    {
        /// <summary>
        /// Constructs a new sequencer.
        /// </summary>
        /// <param name="viewSource">The underlying view source.</param>
        public ViewSourceSequencer(ArrayView<T> viewSource)
        {
            ViewSource = viewSource;
        }

        /// <summary>
        /// Returns the data source of this sequence.
        /// </summary>
        public ArrayView<T> ViewSource { get; }

        /// <summary>
        /// Returns the i-th element of the attached <see cref="ViewSource"/>.
        /// </summary>
        public readonly T ComputeSequenceElement(Index1 sequenceIndex) =>
            ViewSource[sequenceIndex];
    }
}
