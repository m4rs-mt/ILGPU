// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Constants.cs
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
        /// Loads the constant null.
        /// </summary>
        private void LoadNull()
        {
            var t = typeof(object);
            CurrentBlock.Push(
                t.MakePointerType(),
                LLVM.ConstNull(Unit.GetType(t)));
        }

        /// <summary>
        /// Loads an int.
        /// </summary>
        private void Load(int value)
        {
            CurrentBlock.Push(
                typeof(int),
                LLVMExtensions.ConstInt(LLVMContext.Int32TypeInContext(), value, true));
        }

        /// <summary>
        /// Loads a long.
        /// </summary>
        private void Load(long value)
        {
            CurrentBlock.Push(
                typeof(long),
                LLVMExtensions.ConstInt(LLVMContext.Int64TypeInContext(), value, true));
        }

        /// <summary>
        /// Loads a float.
        /// </summary>
        private void Load(float value)
        {
            CurrentBlock.Push(
                typeof(float),
                LLVM.ConstReal(LLVMContext.FloatTypeInContext(), value));
        }

        /// <summary>
        /// Loads a double.
        /// </summary>
        private void Load(double value)
        {
            CurrentBlock.Push(
                typeof(double),
                LLVM.ConstReal(LLVMContext.DoubleTypeInContext(), value));
        }

        /// <summary>
        /// Loads a string.
        /// </summary>
        private void LoadString(string value)
        {
            CurrentBlock.Push(
                typeof(string),
                InstructionBuilder.CreateGlobalStringPtr(value, string.Empty));
        }
    }
}
