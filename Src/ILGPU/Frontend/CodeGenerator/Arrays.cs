// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Creates a new n-dimensional array.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="extent">The array extent.</param>
        /// <param name="elementType">The element type.</param>
        internal ValueReference CreateArray(
            IRBuilder builder,
            ValueReference extent,
            TypeNode elementType)
        {
            // We have to create an appropriate array view type
            // that uses the correct index type to access all elements
            var extentType = extent.Type as ArrayType;
            Debug.Assert(extentType != null, "Invalid extent type");
            var arraySize = builder.CreateArrayAccumulationMultiply(extent);

            // Allocate memory and create the view
            var memoryPtr = CreateTempAlloca(arraySize, elementType);
            var array = builder.CreateNewView(memoryPtr, arraySize);

            // Create the actual array instance
            return builder.CreateArrayImplementation(array, extent);
        }

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
            ValueReference length = block.PopInt(ConvertFlags.None);
            var extent = builder.CreateArrayImplementationExtent(
                ImmutableArray.Create(length),
                0);
            var value = CreateArray(builder, extent, type);
            block.Push(value);
        }

        /// <summary>
        /// Realizes an array load-element operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="elementType">The element type to load.</param>
        private void MakeLoadElement(
            Block block,
            IRBuilder builder,
            Type elementType)
        {
            var typeNode = builder.CreateType(elementType);
            var address = CreateLoadElementAddress(
                block,
                builder,
                typeNode);
            var value = CreateLoad(
                builder,
                address,
                typeNode,
                ConvertFlags.None);
            block.Push(value);
        }

        /// <summary>
        /// Creates a new load-element-address operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="typeNode">The element type to load.</param>
        /// <returns>The loaded element address.</returns>
        private static ValueReference CreateLoadElementAddress(
            Block block,
            IRBuilder builder,
            TypeNode typeNode)
        {
            var index = block.PopInt(ConvertFlags.None);
            var array = block.Pop();
            var view = builder.CreateGetArrayImplementationView(array);
            var targetView = builder.CreateViewCast(view, typeNode);
            return builder.CreateLoadElementAddress(targetView, index, FieldAccessChain.Empty);
        }

        /// <summary>
        /// Realizes an array load-element-address operation.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="elementType">The element type to load.</param>
        private static void MakeLoadElementAddress(
            Block block,
            IRBuilder builder,
            Type elementType)
        {
            var typeNode = builder.CreateType(elementType);
            var address = CreateLoadElementAddress(
                block,
                builder,
                typeNode);
            block.Push(address);
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
            var address = CreateLoadElementAddress(
                block,
                builder,
                typeNode);
            CreateStore(builder, address, value);
        }

        /// <summary>
        /// Realizes an array length value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private static void MakeLoadArrayLength(Block block, IRBuilder builder)
        {
            var array = block.Pop();
            var length = builder.CreateGetLinearArrayImplementationLength(array);
            block.Push(length);
        }
    }
}
