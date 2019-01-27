// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Shuffle.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.ShuffleOperations
{
    /// <summary>
    /// Represents an abstract shuffle operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the shuffle operation.</typeparam>
    public interface IShuffle<T>
        where T : struct
    {
        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        T Shuffle(T variable, int sourceLane);
    }

    /// <summary>
    /// Represents an abstract shuffle-down operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the shuffle operation.</typeparam>
    public interface IShuffleDown<T>
        where T : struct
    {
        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        T ShuffleDown(T variable, int delta);
    }

    /// <summary>
    /// Represents an abstract shuffle-up operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the shuffle operation.</typeparam>
    public interface IShuffleUp<T>
        where T : struct
    {
        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        T ShuffleUp(T variable, int delta);
    }

    /// <summary>
    /// Represents an abstract shuffle-xor operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the shuffle operation.</typeparam>
    public interface IShuffleXor<T>
        where T : struct
    {
        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        T ShuffleXor(T variable, int mask);
    }
}
