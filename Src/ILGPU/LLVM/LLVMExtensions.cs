// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: LLVMExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.LLVM
{
    partial class LLVMMethods
    {
        public const string ExtensionsLibraryName = "ILGPU.LLVM.dll";

        [DllImport(ExtensionsLibraryName, EntryPoint = "PreparePTXModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PreparePTXModule(LLVMModuleRef module, LLVMValueRef entryPoint, LLVMBool ftz, LLVMBool fm);
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
