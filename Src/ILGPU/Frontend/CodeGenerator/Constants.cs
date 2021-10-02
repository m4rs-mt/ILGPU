// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Loads an int.
        /// </summary>
        /// <param name="value">The value.</param>
        private void Load(int value) =>
            Block.Push(Builder.CreatePrimitiveValue(Location, value));

        /// <summary>
        /// Loads a long.
        /// </summary>
        /// <param name="value">The value.</param>
        private void Load(long value) =>
            Block.Push(Builder.CreatePrimitiveValue(Location, value));

        /// <summary>
        /// Loads a float.
        /// </summary>
        /// <param name="value">The value.</param>
        private void Load(float value) =>
            Block.Push(Builder.CreatePrimitiveValue(Location, value));

        /// <summary>
        /// Loads a double.
        /// </summary>
        /// <param name="value">The value.</param>
        private void Load(double value) =>
            Block.Push(Builder.CreatePrimitiveValue(Location, value));

        /// <summary>
        /// Loads a string.
        /// </summary>
        /// <param name="value">The value.</param>
        private void LoadString(string value) =>
            Block.Push(Builder.CreatePrimitiveValue(Location, value));
    }
}
