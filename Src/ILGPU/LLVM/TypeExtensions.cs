// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.LLVM
{
    partial struct LLVMBool
    {
        public static implicit operator bool(LLVMBool b)
        {
            return b.Value != 0 ? true : false;
        }

        public static implicit operator LLVMBool(bool b)
        {
            return b ? new LLVMBool(1) : new LLVMBool(0);
        }
    }

    partial struct LLVMContextRef
    {
        public LLVMTypeRef Int1Type => LLVMMethods.Int1TypeInContext(this);
        public LLVMTypeRef Int8Type => LLVMMethods.Int8TypeInContext(this);
        public LLVMTypeRef Int16Type => LLVMMethods.Int16TypeInContext(this);
        public LLVMTypeRef Int32Type => LLVMMethods.Int32TypeInContext(this);
        public LLVMTypeRef Int64Type => LLVMMethods.Int64TypeInContext(this);
        public LLVMTypeRef FloatType => LLVMMethods.FloatTypeInContext(this);
        public LLVMTypeRef DoubleType => LLVMMethods.DoubleTypeInContext(this);
        public LLVMTypeRef VoidType => LLVMMethods.VoidTypeInContext(this);

        public LLVMTypeRef VoidPtrType => LLVMMethods.PointerType(Int8Type);
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
