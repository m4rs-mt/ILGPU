// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Utilities.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Util
{
    /// <summary>
    /// General util methods.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Swaps the given values.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="first">The first value to swap with the second one.</param>
        /// <param name="second">The second value to swap with the first one.</param>
        public static void Swap<T>(ref T first, ref T second)
        {
            T temp = first;
            first = second;
            second = temp;
        }

        /// <summary>
        /// Swaps the given values iff swap is true.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="performSwap">True, iff the values should be swapped.</param>
        /// <param name="first">The first value to swap with the second one.</param>
        /// <param name="second">The second value to swap with the first one.</param>
        /// <returns>True, iff the values were swapped.</returns>
        public static bool Swap<T>(bool performSwap, ref T first, ref T second)
        {
            if (!performSwap)
                return false;
            Swap(ref first, ref second);
            return true;
        }
    }
}
