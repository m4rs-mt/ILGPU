// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Objects.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Reflection;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a boxing operation that boxes a value.
        /// </summary>
        private void MakeBox()
        {
            var value = CurrentBlock.Pop();
            if (!value.ValueType.IsValueType)
                throw CompilationContext.GetInvalidILCodeException();
            throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedBoxing);
        }

        /// <summary>
        /// Realizes an un-boxing operation that unboxes a previously boxed value.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeUnbox(Type type)
        {
            var value = CurrentBlock.Pop();
            if (!value.ValueType.IsPointer || !type.IsValueType)
                throw CompilationContext.GetInvalidILCodeException();
            throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedUnboxing);
        }

        /// <summary>
        /// Realizes a new-object operation that creates a new instance of a specified type.
        /// </summary>
        /// <param name="method">The target method.</param>
        private void MakeNewObject(MethodBase method)
        {
            var constructor = method as ConstructorInfo;
            if (constructor == null)
                throw CompilationContext.GetInvalidILCodeException();
            var type = constructor.DeclaringType;
            var llvmType = Unit.GetType(type);

            // Use a temporary alloca to realize object creation
            // and initialize object with zero
            var tempAlloca = CreateTempAlloca(llvmType);
            BuildStore(Builder, ConstNull(llvmType), tempAlloca);

            // Invoke constructor for type
            Value[] values = new Value[constructor.GetParameters().Length + 1];
            values[0] = new Value(type.MakePointerType(), tempAlloca);
            CurrentBlock.PopMethodArgs(constructor, values, 1);
            CreateCall(constructor, values);

            // Push created instance on the stack
            if (type.IsValueType)
                CurrentBlock.Push(type, BuildLoad(Builder, values[0].LLVMValue, string.Empty));
            else
                CurrentBlock.Push(values[0]);
        }

        /// <summary>
        /// Realizes a managed-object initialization.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeInitObject(Type type)
        {
            if (type == null)
                throw CompilationContext.GetInvalidILCodeException();

            var address = CurrentBlock.Pop();

            var llvmType = Unit.GetType(type);
            BuildStore(Builder, ConstNull(llvmType), address.LLVMValue);
        }

        /// <summary>
        /// Realizes an is-instance instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeIsInstance(Type type)
        {
            throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedIsInstance);
        }

        /// <summary>
        /// Realizes an indirect load instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeLoadObject(Type type)
        {
            var address = CurrentBlock.Pop();
            address = CreateConversion(address, type.MakePointerType());
            CurrentBlock.Push(type, BuildLoad(Builder, address.LLVMValue, string.Empty));
        }

        /// <summary>
        /// Realizes an indirect store instruction.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void MakeStoreObject(Type type)
        {
            var value = CurrentBlock.Pop(type);
            var address = CurrentBlock.Pop();
            address = CreateConversion(address, type.MakePointerType());
            BuildStore(Builder, value.LLVMValue, address.LLVMValue);
        }
    }
}
