﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
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
            // Setup length argument
            var arguments = InlineList<ValueReference>.Create(
                Builder.CreateConvertToInt32(
                    Location,
                    Block.PopInt(Location, ConvertFlags.None)));
            var array = Builder.CreateNewArray(
                Location,
                Builder.CreateType(elementType, MemoryAddressSpace.Generic),
                ref arguments);

            // Clear array data
            var callArguments = InlineList<ValueReference>.Create(
                Builder.CreateGetViewFromArray(
                    Location,
                    array));
            CreateCall(
                LocalMemory.GetClearMethod(elementType),
                ref callArguments);

            // Push array instance
            Block.Push(array);
        }

        /// <summary>
        /// Creates an array element address load.
        /// </summary>
        /// <param name="elementType">The element type to load.</param>
        /// <param name="type">The IR element type to load.</param>
        /// <returns>The loaded array element address.</returns>
        private Value CreateLoadArrayElementAddress(Type elementType, out TypeNode type)
        {
            var index = Block.PopInt(Location, ConvertFlags.None);
            var array = Block.Pop();

            type = Builder.CreateType(elementType);

            var indices = InlineList<ValueReference>.Create(index);
            var address = Builder.CreateGetArrayElementAddress(
                Location,
                array,
                ref indices);
            return address;
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="elementType">The element type to load.</param>
        private void MakeLoadElementAddress(Type elementType) =>
            Block.Push(
                CreateLoadArrayElementAddress(elementType, out var _));

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="elementType">The element type to load.</param>
        private void MakeLoadElement(Type elementType) =>
            Block.Push(CreateLoad(
                CreateLoadArrayElementAddress(elementType, out var type),
                type,
                elementType.ToTargetUnsignedFlags()));

        /// <summary>
        /// Realizes an array store-element operation.
        /// </summary>
        /// <param name="elementType">The element type to store.</param>
        private void MakeStoreElement(Type elementType)
        {
            var value = Block.Pop();

            CreateStore(
                CreateLoadArrayElementAddress(elementType, out var type),
                CreateConversion(
                    value,
                    type,
                    elementType.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes an array length value.
        /// </summary>
        private void MakeLoadArrayLength()
        {
            var array = Block.Pop();
            var length = Builder.CreateGetArrayLength(
                Location,
                array);
            Block.Push(length);
        }
    }
}
