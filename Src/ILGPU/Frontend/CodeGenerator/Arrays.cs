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
            // Redirect call to the LocalMemory class to allocate an intialized array
            var allocateZeroMethod = LocalMemory.GetAllocateZeroMethod(elementType);

            // Setup length argument
            var arguments = InlineList<ValueReference>.Create(
                Builder.CreateConvertToInt64(
                    Location,
                    Block.PopInt(Location, ConvertFlags.None)));
            CreateCall(allocateZeroMethod, ref arguments);
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
            var arrayView = Block.Pop();

            type = Builder.CreateType(elementType);
            var castedView = Builder.CreateViewCast(Location, arrayView, type);
            return Builder.CreateLoadElementAddress(
                Location,
                castedView,
                index);
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
            var view = Block.Pop();
            var length = Builder.CreateGetViewLength(
                Location,
                view);
            Block.Push(length);
        }
    }
}
