// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: IScanReduceOperation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

namespace ILGPU.Algorithms.ScanReduceOperations
{
    /// <summary>
    /// Implements a scan or a reduction operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    public interface IScanReduceOperation<T>
        where T : struct
    {
        /// <summary>
        /// Returns the associated OpenCL command suffix for the internal code generator
        /// to build the final OpenCL command to use.
        /// </summary>
        string CLCommand { get; }

        /// <summary>
        /// Returns the identity value (the neutral element of the operation), such that
        /// Apply(Apply(Identity, left), right) == Apply(left, right).
        /// </summary>
        T Identity { get; }

        /// <summary>
        /// Applies the current operation.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <returns>The result of the operation.</returns>
        T Apply(T first, T second);

        /// <summary>
        /// Performs an atomic operation of the form target = AtomicUpdate(target.Value, value).
        /// </summary>
        /// <param name="target">The target address to update.</param>
        /// <param name="value">The value.</param>
        void AtomicApply(ref T target, T value);
    }

    /// <summary>
    /// Holds the left and the right boundary of a scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct ScanBoundaries<T>
        where T : struct
    {
        #region Instance

        /// <summary>
        /// Constructs a new scan-boundaries instance.
        /// </summary>
        /// <param name="leftBoundary">The left boundary.</param>
        /// <param name="rightBoundary">The right boundary.</param>
        public ScanBoundaries(T leftBoundary, T rightBoundary)
        {
            LeftBoundary = leftBoundary;
            RightBoundary = rightBoundary;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The left boundary.
        /// </summary>
        public T LeftBoundary { get; }

        /// <summary>
        /// The right boundary.
        /// </summary>
        public T RightBoundary { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of these boundary values.
        /// </summary>
        /// <returns>The string representation of these boundary values.</returns>
        public override string ToString() =>
            $"[{LeftBoundary}, {RightBoundary}]";

        #endregion
    }
}
