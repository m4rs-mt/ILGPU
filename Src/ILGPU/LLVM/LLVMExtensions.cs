// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: LLVMExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.LLVM
{
    partial class LLVMMethods
    {
        static LLVMMethods()
        {
            DLLLoader.AddDefaultX86X64SearchPath();
        }

        [DllImport(LibraryName, EntryPoint = "ILGPU_PreparePTXModule", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PreparePTXModule(LLVMModuleRef module, LLVMValueRef entryPoint, LLVMBool ftz, LLVMBool fm);
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
