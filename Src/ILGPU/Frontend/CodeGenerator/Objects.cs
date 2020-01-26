// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Objects.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Reflection;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a boxing operation that boxes a value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private void MakeBox(Block block, IRBuilder builder)
        {
            var value = block.Pop();
            if (!value.Type.IsObjectType)
                throw this.GetInvalidILCodeException();
            var alloca = CreateTempAlloca(value.Type);
            CreateStore(builder, alloca, value);
        }

        /// <summary>
        /// Realizes an un-boxing operation that unboxes a previously boxed value.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="type">The target type.</param>
        private void MakeUnbox(
            Block block,
            IRBuilder builder,
            Type type)
        {
            if (type == null || !type.IsValueType)
                throw this.GetInvalidILCodeException();
            var address = block.Pop();
            var typeNode = builder.CreateType(type);
            block.Push(CreateLoad(
                builder,
                address,
                typeNode,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes a new-object operation that creates a new instance of a specified type.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="method">The target method.</param>
        private void MakeNewObject(
            Block block,
            IRBuilder builder,
            MethodBase method)
        {
            var constructor = method as ConstructorInfo;
            if (constructor == null)
                throw this.GetInvalidILCodeException();

            var type = constructor.DeclaringType;
            var typeNode = builder.CreateType(type);
            var alloca = CreateTempAlloca(typeNode);

            var value = builder.CreateNull(typeNode);
            CreateStore(builder, alloca, value);

            // Invoke constructor for type
            var values = block.PopMethodArgs(method, alloca);
            CreateCall(block, builder, constructor, values);

            // Push created instance on the stack
            block.Push(CreateLoad(
                builder,
                alloca,
                typeNode,
                ConvertFlags.None));
        }

        /// <summary>
        /// Realizes a managed-object initialization.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="type">The target type.</param>
        private void MakeInitObject(
            Block block,
            IRBuilder builder,
            Type type)
        {
            if (type == null)
                throw this.GetInvalidILCodeException();

            var address = block.Pop();
            var typeNode = builder.CreateType(type);
            var value = builder.CreateNull(typeNode);
            CreateStore(builder, address, value);
        }

        /// <summary>
        /// Realizes an is-instance instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="type">The target type.</param>
        private void MakeIsInstance(Block block, Type type)
        {
            throw this.GetNotSupportedException(ErrorMessages.NotSupportedIsInstance);
        }

        /// <summary>
        /// Realizes an indirect load instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="type">The target type.</param>
        private void MakeLoadObject(
            Block block,
            IRBuilder builder,
            Type type)
        {
            var address = block.Pop();
            var targetElementType = builder.CreateType(type);
            block.Push(CreateLoad(
                builder,
                address,
                targetElementType,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes an indirect store instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="type">The target type.</param>
        private void MakeStoreObject(
            Block block,
            IRBuilder builder,
            Type type)
        {
            var typeNode = builder.CreateType(type);
            var value = block.Pop(typeNode, ConvertFlags.None);
            var address = block.Pop();
            CreateStore(builder, address, value);
        }
    }
}
