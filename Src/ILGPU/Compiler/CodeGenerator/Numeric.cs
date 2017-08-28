// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Numerics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Util;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a numeric-and instruction.
        /// </summary>
        private void MakeNumericAnd()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, BuildAnd(Builder, left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-or instruction.
        /// </summary>
        private void MakeNumericOr()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, BuildOr(Builder, left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-xor instruction.
        /// </summary>
        private void MakeNumericXor()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, BuildXor(Builder, left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-shl instruction.
        /// </summary>
        private void MakeNumericShl()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, BuildShl(Builder, left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-shr instruction.
        /// </summary>
        /// <param name="forceUnsigned">True if the comparison should be forced to be unsigned.</param>
        private void MakeNumericShr(bool forceUnsigned = false)
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            if (forceUnsigned || type.IsUnsignedInt())
                CurrentBlock.Push(type, BuildLShr(Builder, left, right, name));
            else
                CurrentBlock.Push(type, BuildAShr(Builder, left, right, name));
        }

        /// <summary>
        /// Realizes a numeric-neg instruction.
        /// </summary>
        private void MakeNumericNeg()
        {
            var value = CurrentBlock.Pop();
            var name = string.Empty;
            if (value.ValueType.IsFloat())
                CurrentBlock.Push(value.ValueType, BuildFNeg(Builder, value.LLVMValue, name));
            else
                CurrentBlock.Push(value.ValueType, BuildNeg(Builder, value.LLVMValue, name));
        }

        /// <summary>
        /// Realizes a numeric-not instruction.
        /// </summary>
        private void MakeNumericNot()
        {
            var value = CurrentBlock.Pop();
            CurrentBlock.Push(value.ValueType, BuildNot(Builder, value.LLVMValue, string.Empty));
        }
    }
}
