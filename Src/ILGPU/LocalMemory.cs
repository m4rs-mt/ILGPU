// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LocalMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime.CPU;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Contains methods to allocate and manage local memory.
    /// </summary>
    public static partial class LocalMemory
    {
        /// <summary>
        /// Allocates a single element in local memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An allocated element in local memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LocalMemoryIntrinsic(LocalMemoryIntrinsicKind.Allocate)]
        public static ArrayView<T> Allocate<T>(int extent)
            where T : unmanaged
        {
            Trace.Assert(extent >= 0, "Invalid extent");
            return CPURuntimeGroupContext.Current.AllocateLocalMemory<T>(extent);
        }
    }
}
