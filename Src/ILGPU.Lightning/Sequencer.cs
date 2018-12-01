// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: Sequencer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Sequencers
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
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    public readonly struct IndexSequencer : ISequencer<Index>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public Index ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a float sequencer.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    public readonly struct FloatSequencer : ISequencer<float>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public float ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a double sequencer.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    public readonly struct DoubleSequencer : ISequencer<double>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index)" />
        public double ComputeSequenceElement(Index sequenceIndex) => sequenceIndex;
    }
}
