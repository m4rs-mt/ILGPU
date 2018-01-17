// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using static ILGPU.LLVM.LLVMMethods;

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
                ConstNull(Unit.GetType(t)));
        }

        /// <summary>
        /// Loads an int.
        /// </summary>
        private void Load(int value)
        {
            CurrentBlock.Push(
                typeof(int),
                ConstInt(LLVMContext.Int32Type, value, true));
        }

        /// <summary>
        /// Loads a long.
        /// </summary>
        private void Load(long value)
        {
            CurrentBlock.Push(
                typeof(long),
                ConstInt(LLVMContext.Int64Type, value, true));
        }

        /// <summary>
        /// Loads a float.
        /// </summary>
        private void Load(float value)
        {
            CurrentBlock.Push(
                typeof(float),
                ConstReal(LLVMContext.FloatType, value));
        }

        /// <summary>
        /// Loads a double.
        /// </summary>
        private void Load(double value)
        {
            if (Unit.Force32BitFloats)
                Load((float)value);
            else
                CurrentBlock.Push(
                    typeof(double),
                    ConstReal(LLVMContext.DoubleType, value));
        }

        /// <summary>
        /// Loads a string.
        /// </summary>
        private void LoadString(string value)
        {
            CurrentBlock.Push(
                typeof(string),
                BuildGlobalStringPtr(Builder, value, string.Empty));
        }
    }
}
