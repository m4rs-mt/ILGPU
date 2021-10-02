// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IComparisonOperation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms.ComparisonOperations
{
    /// <summary>
    /// Implements a comparison operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the comparison operation</typeparam>
    public interface IComparisonOperation<T>
        where T : struct
    {
        /// <summary>
        /// Compares two elements.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <returns>
        /// Less than zero, if first is less than second.
        /// Zero, if first is equal to second.
        /// Greater than zero, if first is greater than second.
        /// </returns>
        int Compare(T first, T second);
    }
}
