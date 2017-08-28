// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: AtomicIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using System;

namespace ILGPU.Compiler.Intrinsic
{
    enum AtomicIntrinsicKind
    {
        Xch = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpXchg,

        Add = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpAdd,
        Sub = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpSub,

        And = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpAnd,
        Or = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpOr,
        Xor = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpXor,

        Max = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpMax,
        Min = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpMin,

        UMax = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpUMax,
        UMin = LLVMAtomicRMWBinOp.LLVMAtomicRMWBinOpUMin,

        CmpXch,

        // The operations below require specific backend support

        AddF32,

        IncU32,
        DecU32,
    }

    /// <summary>
    /// Marks intrinsic atomic methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class AtomicIntrinsicAttribute : IntrinsicAttribute
    {
        public AtomicIntrinsicAttribute(AtomicIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Atomic;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public AtomicIntrinsicKind IntrinsicKind { get; }
    }
}
