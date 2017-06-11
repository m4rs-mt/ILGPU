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

using LLVMSharp;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPU.Util
{
    static class LLVMExtensions
    {
        private static readonly FieldInfo IRBuilderInstanceField = typeof(IRBuilder).GetField("instance", BindingFlags.NonPublic | BindingFlags.Instance);

        public static LLVMBuilderRef GetBuilderRef(this IRBuilder builder)
        {
            return (LLVMBuilderRef)IRBuilderInstanceField.GetValue(builder);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass")]
        [DllImport(NativeMethods.LLVMExtensionsLibName, EntryPoint = "PreparePTXModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PreparePTXModule(LLVMModuleRef module, LLVMValueRef entryPoint, LLVMBool ftz, LLVMBool fm);

        public static unsafe LLVMValueRef ConstInt(LLVMTypeRef @IntTy, long @N, LLVMBool @SignExtend)
        {
            return LLVM.ConstInt(IntTy, *(ulong*)&N, SignExtend);
        }

        public static LLVMValueRef ConstStringInContext(LLVMContextRef Context, string Str, LLVMBool @DontNullTerminate)
        {
            return LLVM.ConstStringInContext(Context, Str, (uint)Str.Length, DontNullTerminate);
        }
    }
}
