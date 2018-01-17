// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Reduction.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
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
}
