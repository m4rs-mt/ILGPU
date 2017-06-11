// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using LLVMSharp;
using System;
using System.Diagnostics;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Computes the array-element address for the given array and the element index.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="index">The target index/</param>
        /// <returns>The computed element address.</returns>
        private LLVMValueRef ComputeArrayAddress(LLVMValueRef array, LLVMValueRef index)
        {
            var baseAddr = InstructionBuilder.CreateExtractValue(array, 0, string.Empty);
            return InstructionBuilder.CreateInBoundsGEP(baseAddr, new LLVMValueRef[] { index }, string.Empty);
        }

        /// <summary>
        /// Realizes a new-array instruction.
        /// </summary>
        /// <param name="elementType">The element type of the array.</param>
        private void MakeNewArray(Type elementType)
        {
            var stackValue = CurrentBlock.Pop();
            var arrayLength = stackValue.LLVMValue;

            if (!arrayLength.IsConstant())
                throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedArrayCreation);

            // Allocate the element data first in a local alloca
            var llvmElementType = Unit.GetType(elementType);
            var arrayLengthValue = (uint)arrayLength.ConstIntGetZExtValue();

            var llvmAllocaArrayType = LLVM.ArrayType(llvmElementType, arrayLengthValue);
            var arrayData = CreateTempAlloca(llvmAllocaArrayType);
            InstructionBuilder.CreateStore(LLVM.ConstNull(llvmAllocaArrayType), arrayData);

            // Array<T> = (T*, int32)
            var arrayType = elementType.MakeArrayType();
            var llvmArrayType = Unit.GetType(arrayType);
            var initialArrayValue = LLVM.GetUndef(llvmArrayType);
            // Setup T*: (T*, undef)
            var basePtr = InstructionBuilder.CreatePointerCast(arrayData, LLVM.PointerType(llvmElementType, 0), string.Empty);
            var arrayWithPointer = InstructionBuilder.CreateInsertValue(initialArrayValue, basePtr, 0, string.Empty);
            // Setup length: (T*, int32)
            var array = InstructionBuilder.CreateInsertValue(arrayWithPointer, arrayLength, 1, string.Empty);

            // Push the final array value
            CurrentBlock.Push(arrayType, array);
        }

        /// <summary>
        /// Loads the length of an array.
        /// </summary>
        private void LoadArrayLength()
        {
            var array = CurrentBlock.Pop();
            Debug.Assert(array.ValueType.IsArray);
            CurrentBlock.Push(typeof(int), InstructionBuilder.CreateExtractValue(array.LLVMValue, 1, "ldlen"));
        }

        /// <summary>
        /// Loads an array element.
        /// </summary>
        /// <param name="type">The type of element to load.</param>
        private void LoadArrayElement(Type type)
        {
            var idx = CurrentBlock.PopInt();
            var array = CurrentBlock.Pop();
            Debug.Assert(array.ValueType.IsArray);
            var addr = ComputeArrayAddress(array.LLVMValue, idx.LLVMValue);
            var value = InstructionBuilder.CreateLoad(addr, string.Empty);
            CurrentBlock.Push(CreateConversion(new Value(array.ValueType.GetElementType(), value), type));
        }

        /// <summary>
        /// Loads the address of an array element.
        /// </summary>
        private void LoadArrayElementAddress()
        {
            var idx = CurrentBlock.PopInt();
            var array = CurrentBlock.Pop();
            Debug.Assert(array.ValueType.IsArray);
            var addr = ComputeArrayAddress(array.LLVMValue, idx.LLVMValue);
            CurrentBlock.Push(array.ValueType.GetElementType().MakePointerType(), addr);
        }

        /// <summary>
        /// Stores an array element of the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        private void StoreArrayElement(Type type)
        {
            var value = CurrentBlock.Pop(type);
            var idx = CurrentBlock.PopInt();
            var array = CurrentBlock.Pop();
            Debug.Assert(array.ValueType.IsArray);
            var addr = ComputeArrayAddress(array.LLVMValue, idx.LLVMValue);
            InstructionBuilder.CreateStore(value.LLVMValue, addr);
        }
    }
}
