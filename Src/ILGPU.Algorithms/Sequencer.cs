// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sequencer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

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
        T ComputeSequenceElement(LongIndex1D sequenceIndex);
    }

    /// <summary>
    /// Represents an identity implementation of an index sequencer.
    /// </summary>
    public readonly struct IndexSequencer : ISequencer<Index1D>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly Index1D ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            (Index1D)sequenceIndex;
    }


    /// <summary>
    /// Represents an identity implementation of a FP8E4M3 sequencer.
    /// </summary>
    public readonly struct FP8E4M3Sequencer : ISequencer<FP8E4M3>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly FP8E4M3 ComputeSequenceElement(LongIndex1D sequenceIndex)
            => (FP8E4M3)sequenceIndex.X;
    }

    /// <summary>
    /// Represents an identity implementation of a FP8E5M2 sequencer.
    /// </summary>
    public readonly struct FP8E5M2Sequencer : ISequencer<FP8E5M2>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly FP8E5M2 ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            (FP8E5M2)sequenceIndex.X;
    }

    /// <summary>
    /// Represents an identity implementation of a BF16 sequencer.
    /// </summary>
    public readonly struct BF16Sequencer : ISequencer<BF16>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly BF16 ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            (BF16)sequenceIndex.X;
    }



    /// <summary>
    /// Represents an identity implementation of a half sequencer.
    /// </summary>
    public readonly struct HalfSequencer : ISequencer<Half>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly Half ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            (Half)sequenceIndex.X;
    }

    /// <summary>
    /// Represents an identity implementation of a float sequencer.
    /// </summary>
    public readonly struct FloatSequencer : ISequencer<float>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly float ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            sequenceIndex;
    }

    /// <summary>
    /// Represents an identity implementation of a double sequencer.
    /// </summary>
    public readonly struct DoubleSequencer : ISequencer<double>
    {
        /// <summary cref="ISequencer{T}.ComputeSequenceElement(LongIndex1D)" />
        public readonly double ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            sequenceIndex;
    }

    /// <summary>
    /// Represents a sequencer that wraps an array view in a sequencer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride of the underlying view.</typeparam>
    public readonly struct ViewSourceSequencer<T, TStride> : ISequencer<T>
        where T : unmanaged
        where TStride : struct, IStride1D
    {
        /// <summary>
        /// Constructs a new sequencer.
        /// </summary>
        /// <param name="viewSource">The underlying view source.</param>
        public ViewSourceSequencer(ArrayView1D<T, TStride> viewSource)
        {
            ViewSource = viewSource;
        }

        /// <summary>
        /// Returns the data source of this sequence.
        /// </summary>
        public ArrayView1D<T, TStride> ViewSource { get; }

        /// <summary>
        /// Returns the i-th element of the attached <see cref="ViewSource"/>.
        /// </summary>
        public readonly T ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            ViewSource[sequenceIndex];
    }
}
