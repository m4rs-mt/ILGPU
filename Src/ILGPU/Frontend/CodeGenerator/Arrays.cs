// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes an array creation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="elementType">The element type.</param>
        private void MakeNewArray(
            Block block,
            IRBuilder builder,
            Type elementType)
        {
            // Resolve length and type
            var type = builder.CreateType(elementType);
            var extent = block.PopInt(ConvertFlags.None);
            var value = builder.CreateArray(type, 1, extent);
            block.Push(value);
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="_">The element type to load.</param>
        private void MakeLoadElementAddress(
            Block block,
            IRBuilder builder,
            Type _)
        {
            // TODO: make sure that element loads are converted properly in all cases
            var index = block.PopInt(ConvertFlags.None);
            var array = block.Pop();
            var linearAddress = builder.ComputeArrayAddress(
                index,
                array,
                0);
            var value = builder.CreateLoadElementAddress(array, linearAddress);
            block.Push(value);
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="_">The element type to load.</param>
        private void MakeLoadElement(
            Block block,
            IRBuilder builder,
            Type _)
        {
            // TODO: make sure that element loads are converted properly in all cases
            var index = block.PopInt(ConvertFlags.None);
            var array = block.Pop();
            var value = builder.CreateGetArrayElement(array, index);
            block.Push(value);
        }

        /// <summary>
        /// Realizes an array store-element operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="elementType">The element type to store.</param>
        private void MakeStoreElement(
            Block block,
            IRBuilder builder,
            Type elementType)
        {
            var typeNode = builder.CreateType(elementType);
            var value = block.Pop(typeNode, ConvertFlags.None);
            var index = block.PopInt(ConvertFlags.None);
            var array = block.Pop();
            builder.CreateSetArrayElement(array, index, value);
        }

        /// <summary>
        /// Realizes an array length value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private static void MakeLoadArrayLength(Block block, IRBuilder builder)
        {
            var array = block.Pop();
            var length = builder.CreateGetArrayLength(array);
            block.Push(length);
        }
    }
}
