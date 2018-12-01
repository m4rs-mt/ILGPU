// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanOperation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------;


namespace ILGPU.ScanOperations
{
    /// <summary>
    /// Implements a scan operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    public interface IScanOperation<T>
        where T : struct
    {
        /// <summary>
        /// Returns the scan's identity value.
        /// </summary>
        T Identity { get; }

        /// <summary>
        /// Applies the scan operation.
        /// </summary>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <returns>The result of the scan operation.</returns>
        T Apply(T first, T second);
    }

    /// <summary>
    /// Represents the scan operation type.
    /// </summary>
    public enum ScanKind
    {
        /// <summary>
        /// An inclusive scan operation.
        /// </summary>
        Inclusive,

        /// <summary>
        /// An exclusive scan operation.
        /// </summary>
        Exclusive
    }
}
