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

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes an array creation.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        private void MakeNewArray(Type elementType)
        {
            // Resolve length and type
            var type = Builder.CreateType(elementType);
            var extent = Block.PopInt(Location, ConvertFlags.None);
            var value = Builder.CreateArray(
                Location,
                type,
                1,
                extent);
            Block.Push(value);
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="_">The element type to load.</param>
        private void MakeLoadElementAddress(Type _)
        {
            // TODO: make sure that element loads are converted properly in all cases
            var index = Block.PopInt(Location, ConvertFlags.None);
            var array = Block.Pop();
            var linearAddress = Builder.ComputeArrayAddress(
                Location,
                index,
                array,
                0);
            var value = Builder.CreateLoadElementAddress(
                Location,
                array,
                linearAddress);
            Block.Push(value);
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="_">The element type to load.</param>
        private void MakeLoadElement(Type _)
        {
            // TODO: make sure that element loads are converted properly in all cases
            var index = Block.PopInt(Location, ConvertFlags.None);
            var array = Block.Pop();
            var value = Builder.CreateGetArrayElement(
                Location,
                array,
                index);
            Block.Push(value);
        }

        /// <summary>
        /// Realizes an array store-element operation.
        /// </summary>
        /// <param name="elementType">The element type to store.</param>
        private void MakeStoreElement(Type elementType)
        {
            var typeNode = Builder.CreateType(elementType);
            var value = Block.Pop(typeNode, ConvertFlags.None);
            var index = Block.PopInt(Location, ConvertFlags.None);
            var array = Block.Pop();
            Builder.CreateSetArrayElement(
                Location,
                array,
                index,
                value);
        }

        /// <summary>
        /// Realizes an array length value.
        /// </summary>
        private void MakeLoadArrayLength()
        {
            var array = Block.Pop();
            var length = Builder.CreateGetArrayLength(Location, array);
            Block.Push(length);
        }
    }
}
