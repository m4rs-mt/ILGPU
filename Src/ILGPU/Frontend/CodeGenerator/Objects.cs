// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Objects.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.IR;
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
        private void MakeBox()
        {
            var value = Block.Pop();
            if (!value.Type.IsObjectType)
                throw Location.GetInvalidOperationException();
            var alloca = CreateTempAlloca(value.Type);
            CreateStore(alloca, value);
        }

        /// <summary>
        /// Realizes an unboxing operation that unboxes a previously boxed value.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeUnbox(Type type)
        {
            if (type == null || !type.IsValueType)
                throw Location.GetInvalidOperationException();
            var address = Block.Pop();
            var typeNode = Builder.CreateType(type);
            Block.Push(CreateLoad(
                address,
                typeNode,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes a new-object operation that creates a new instance of a specified
        /// type.
        /// </summary>
        /// <param name="method">The target method.</param>
        private void MakeNewObject(MethodBase method)
        {
            var constructor = method as ConstructorInfo;
            if (constructor == null)
                throw Location.GetInvalidOperationException();

            var type = constructor.DeclaringType.AsNotNull();
            var typeNode = Builder.CreateType(type);
            var alloca = CreateTempAlloca(typeNode);

            var value = Builder.CreateNull(Location, typeNode);
            CreateStore(alloca, value);

            // Invoke constructor for type
            var values = Block.PopMethodArgs(Location, method, alloca);
            CreateCall(constructor, ref values);

            // Push created instance on the stack
            Block.Push(CreateLoad(
                alloca,
                typeNode,
                ConvertFlags.None));
        }

        /// <summary>
        /// Realizes a managed-object initialization.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeInitObject(Type type)
        {
            if (type == null)
                throw Location.GetInvalidOperationException();

            var address = Block.Pop();
            var typeNode = Builder.CreateType(type);
            var value = Builder.CreateNull(Location, typeNode);
            CreateStore(address, value);
        }

        /// <summary>
        /// Realizes an is-instance instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeIsInstance(Type type) =>
            throw Location.GetNotSupportedException(
                ErrorMessages.NotSupportedIsInstance);

        /// <summary>
        /// Realizes an indirect load instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeLoadObject(Type type)
        {
            var address = Block.Pop();
            var targetElementType = Builder.CreateType(type);
            Block.Push(CreateLoad(
                address,
                targetElementType,
                type.ToTargetUnsignedFlags()));
        }

        /// <summary>
        /// Realizes an indirect store instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeStoreObject(Type type)
        {
            var typeNode = Builder.CreateType(type);
            var value = Block.Pop(typeNode, ConvertFlags.None);
            var address = Block.Pop();
            CreateStore(address, value);
        }

        /// <summary>
        /// Loads the size of the type (in bytes).
        /// </summary>
        /// <param name="type">The target type.</param>
        private void LoadSizeOf(Type type)
        {
            // Pushes the size, in bytes, of a supplied value type onto the evaluation
            // stack.
            //
            // For a reference type, the size returned is the size of a reference value
            // of the corresponding type (4 bytes on 32-bit systems), not the size of the
            // data stored in objects referred to by the reference value. A generic type
            // parameter can be used only in the body of the type or method that defines
            // it. When that type or method is instantiated, the generic type parameter
            // is replaced by a value type or reference type.
            if (type.IsValueType)
            {
                Load(Interop.SizeOf(type));
            }
            else
            {
                var pointerSize = TypeContext.TargetPlatform.Is64Bit() ? 8 : 4;
                Load(pointerSize);
            }
        }
    }
}
