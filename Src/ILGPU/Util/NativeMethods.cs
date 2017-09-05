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

using ILGPU.LLVM;
using ILGPU.Resources;
using System;

namespace ILGPU.Util
{
    static class NativeMethods
    {
        private static bool LLVMLibLoaded = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LLVM")]
        public static void LoadLLVMLib()
        {
            if (LLVMLibLoaded)
                return;
            LLVMLibLoaded = true;

            if (!DLLLoader.LoadLib(LLVMMethods.LibraryName))
                throw new InvalidOperationException(ErrorMessages.CannotLoadLLVMLib);
        }
    }
}
