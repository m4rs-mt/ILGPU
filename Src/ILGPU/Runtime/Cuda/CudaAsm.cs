// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaAsm.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Provides library calls for inline PTX assembly instructions.
    /// </summary>
    public static partial class CudaAsm
    {
        /// <summary>
        /// Returns true if running on a Cuda accelerator.
        /// </summary>
        public static bool IsSupported
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Accelerator.CurrentType == AcceleratorType.Cuda;
        }

        /// <summary>
        /// Writes the inline PTX assembly instructions into the kernel.
        /// </summary>
        /// <param name="asm">The PTX assembly instruction string.</param>
        [LanguageIntrinsic(LanguageIntrinsicKind.EmitPTX)]
        public static void Emit(string asm) =>
            throw new NotImplementedException();
    }
}
