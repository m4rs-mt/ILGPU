// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: Sequencer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------


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
        /// Computes the sequence element for the corresponding <paramref name="sequenceIndex"/>.
        /// </summary>
        /// <param name="sequenceIndex">The sequence index for the computation of the corresponding value.</param>
        /// <returns>The computed sequence value.</returns>
        T ComputeSequenceElement(Index sequenceIndex);
    }

    /// <summary>
    /// Represents an identity implementation of an index sequencer.
    /// </summary>
    public readonly struct IndexSequencer : ISequencer<Index>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public Index ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a float sequencer.
    /// </summary>
    public readonly struct FloatSequencer : ISequencer<float>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public float ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a double sequencer.
    /// </summary>
    public readonly struct DoubleSequencer : ISequencer<double>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public double ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }
}
