// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReductionOperation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

namespace ILGPU.ReductionOperations
{
    /// <summary>
    /// Represents an abstract interface for a value reduction.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    public interface IReduction<T>
        where T : struct
    {
        /// <summary>
        /// Returns the neutral element of this reduction operation, such that
        /// Reduce(Reduce(neutralElement, left), right) == Reduce(left, right).
        /// </summary>
        T NeutralElement { get; }

        /// <summary>
        /// Performs a reduction of the form result = Reduce(left, right).
        /// </summary>
        /// <param name="left">The left value of the reduction.</param>
        /// <param name="right">The right value of the reduction.</param>
        /// <returns>The result of the reduction.</returns>
        T Reduce(T left, T right);
    }

    /// <summary>
    /// Represents an abstract interface for a atomic value reduction.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    public interface IAtomicReduction<T> : IReduction<T>
        where T : struct
    {
        /// <summary>
        /// Performs an atomic reduction of the form target = AtomicUpdate(target.Value, value).
        /// </summary>
        /// <param name="target">The target address to update.</param>
        /// <param name="value">The value.</param>
        void AtomicReduce(ref T target, T value);
    }
}
