// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a convert instruction.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        private void MakeConvert(Type targetType, bool forceUnsigned = false)
        {
            var value = CurrentBlock.Pop();
            CurrentBlock.Push(CreateConversion(value, targetType, forceUnsigned));
        }

        /// <summary>
        /// Conerts the given value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        public Value CreateConversion(Value value, Type targetType, bool forceUnsigned = false)
        {
            var llvmType = Unit.GetType(targetType);
            var valueBasicValueType = value.BasicValueType;
            var targetBasicValueType = targetType.GetBasicValueType();
            if (value.ValueType == targetType ||
                ((valueBasicValueType != BasicValueType.Ptr || targetBasicValueType != BasicValueType.Ptr)
                    && valueBasicValueType == targetBasicValueType))
                return value;
            var name = string.Empty;
            LLVMValueRef targetValue;
            if (valueBasicValueType.IsInt())
            {
                if (targetBasicValueType == BasicValueType.Ptr)
                    targetValue = BuildIntToPtr(Builder, value.LLVMValue, llvmType, name);
                else if (targetBasicValueType.IsFloat())
                {
                    if (forceUnsigned || targetBasicValueType.IsUnsignedInt())
                        targetValue = BuildUIToFP(Builder, value.LLVMValue, llvmType, name);
                    else
                        targetValue = BuildSIToFP(Builder, value.LLVMValue, llvmType, name);
                }
                else if (valueBasicValueType > targetBasicValueType)
                    targetValue = BuildTrunc(Builder, value.LLVMValue, llvmType, name);
                else if (valueBasicValueType < targetBasicValueType)
                {
                    if (forceUnsigned || valueBasicValueType.IsUnsignedInt() || valueBasicValueType == BasicValueType.UInt1)
                        targetValue = BuildZExt(Builder, value.LLVMValue, llvmType, name);
                    else
                        targetValue = BuildSExt(Builder, value.LLVMValue, llvmType, name);
                }
                else
                    throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedIntConversion);
            }
            else if (valueBasicValueType.IsFloat())
            {
                if (targetBasicValueType.IsInt())
                {
                    if (forceUnsigned || targetBasicValueType.IsUnsignedInt())
                        targetValue = BuildFPToUI(Builder, value.LLVMValue, llvmType, name);
                    else
                        targetValue = BuildFPToSI(Builder, value.LLVMValue, llvmType, name);
                }
                else if (valueBasicValueType > targetBasicValueType)
                    targetValue = BuildFPTrunc(Builder, value.LLVMValue, llvmType, name);
                else if (valueBasicValueType < targetBasicValueType)
                    targetValue = BuildFPExt(Builder, value.LLVMValue, llvmType, name);
                else
                    throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedFloatConversion);
            }
            else if (valueBasicValueType == BasicValueType.Ptr)
            {
                if (targetBasicValueType.IsInt())
                {
                    var intPtr = BuildPtrToInt(Builder, value.LLVMValue, Unit.NativeIntPtrType, name);
                    return CreateConversion(new Value(Unit.IntPtrType, intPtr), targetType);
                }
                else if (targetBasicValueType == BasicValueType.Ptr)
                    targetValue = BuildBitCast(Builder, value.LLVMValue, llvmType, name);
                else
                    throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedPointerConversion);
            }
            else
                throw CompilationContext.GetNotSupportedException(ErrorMessages.NotSupportedConversion);
            return new Value(targetType, targetValue);
        }

        /// <summary>
        /// Realizes a cast operation that casts a given class to another type.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        private void MakeCastClass(Type targetType)
        {
            var value = CurrentBlock.Pop();
            CurrentBlock.Push(CreateCastClass(value, targetType));
        }

        /// <summary>
        /// Conerts the given class value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        public Value CreateCastClass(Value value, Type targetType)
        {
            if (targetType.IsInterface)
                throw new NotSupportedException(); // Not supported for now
            while (value.ValueType != targetType)
            {
                if (value.ValueType.BaseType == null)
                    throw new InvalidOperationException();
                value = new Value(
                    value.ValueType.BaseType,
                    BuildStructGEP(Builder, value.LLVMValue, 0, string.Empty));
            }
            return value;
        }
    }
}
