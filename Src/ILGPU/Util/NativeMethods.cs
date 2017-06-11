// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: NativeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Util
{
    static class NativeMethods
    {
#if WIN
        internal const string LLVMLibName = "libLLVM.dll";
        internal const string LLVMExtensionsLibName = "ILGPU.LLVM.dll";
#else
        internal const string LLVMLibName = "libLLVM.so";
        internal const string LLVMExtensionsLibName = "ILGPU.LLVM.so";
#endif

        private static bool LLVMLibLoaded = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LLVM")]
        public static void LoadLLVMLib()
        {
            if (LLVMLibLoaded)
                return;
            LLVMLibLoaded = true;

            if (!DLLLoader.LoadLib(LLVMLibName) ||
                !DLLLoader.LoadLib(LLVMExtensionsLibName))
                throw new InvalidOperationException(ErrorMessages.CannotLoadLLVMLib);
        }
    }
}
