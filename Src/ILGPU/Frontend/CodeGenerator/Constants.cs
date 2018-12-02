// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Loads an int.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        private static void Load(
            Block block,
            IRBuilder builder,
            int value)
        {
            block.Push(builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a long.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        private static void Load(
            Block block,
            IRBuilder builder,
            long value)
        {
            block.Push(builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a float.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        private static void Load(
            Block block,
            IRBuilder builder,
            float value)
        {
            block.Push(builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a double.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        private static void Load(
            Block block,
            IRBuilder builder,
            double value)
        {
            block.Push(builder.CreatePrimitiveValue(value));
        }

        /// <summary>
        /// Loads a string.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="value">The value.</param>
        private static void LoadString(
            Block block,
            IRBuilder builder,
            string value)
        {
            block.Push(builder.CreatePrimitiveValue(value));
        }

    }
}
