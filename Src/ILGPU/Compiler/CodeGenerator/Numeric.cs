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

using ILGPU.Util;
using LLVMSharp;

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
            CurrentBlock.Push(type, InstructionBuilder.CreateAnd(left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-or instruction.
        /// </summary>
        private void MakeNumericOr()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, InstructionBuilder.CreateOr(left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-xor instruction.
        /// </summary>
        private void MakeNumericXor()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, InstructionBuilder.CreateXor(left, right, string.Empty));
        }

        /// <summary>
        /// Realizes a numeric-shl instruction.
        /// </summary>
        private void MakeNumericShl()
        {
            var type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            CurrentBlock.Push(type, InstructionBuilder.CreateShl(left, right, string.Empty));
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
                CurrentBlock.Push(type, InstructionBuilder.CreateLShr(left, right, name));
            else
                CurrentBlock.Push(type, InstructionBuilder.CreateAShr(left, right, name));
        }

        /// <summary>
        /// Realizes a numeric-neg instruction.
        /// </summary>
        private void MakeNumericNeg()
        {
            var value = CurrentBlock.Pop();
            var name = string.Empty;
            if (value.ValueType.IsFloat())
                CurrentBlock.Push(value.ValueType, InstructionBuilder.CreateFNeg(value.LLVMValue, name));
            else
                CurrentBlock.Push(value.ValueType, InstructionBuilder.CreateNeg(value.LLVMValue, name));
        }

        /// <summary>
        /// Realizes a numeric-not instruction.
        /// </summary>
        private void MakeNumericNot()
        {
            var value = CurrentBlock.Pop();
            CurrentBlock.Push(value.ValueType, InstructionBuilder.CreateNot(value.LLVMValue, string.Empty));
        }
    }
}
