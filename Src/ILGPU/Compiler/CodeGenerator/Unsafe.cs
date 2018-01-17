// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Unsafe.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a local stackalloc instruction.
        /// </summary>
        private void MakeLocalAlloc()
        {
            var size = CurrentBlock.Pop();
            var arrayLength = size.LLVMValue;
            if (!IsConstant(arrayLength))
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedUnsafeAllocation);
            // Allocate the element data first in a local alloca
            var llvmElementType = LLVMContext.Int8Type;
            var arrayData = BuildArrayAlloca(Builder, llvmElementType, arrayLength, "localloc");
            CurrentBlock.Push(typeof(byte).MakePointerType(), arrayData);
        }

    }
}
