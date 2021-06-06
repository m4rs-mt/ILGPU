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
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Contains methods to allocate and manage local memory.
    /// </summary>
    public static partial class LocalMemory
    {
        /// <summary>
        /// A readonly reference to the <see cref="Clear{T}(ArrayView{T})"/> method.
        /// </summary>
        private static readonly MethodInfo ClearMethod =
            typeof(LocalMemory).GetMethod(
                nameof(Clear),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Creates a typed <see cref="Clear{T}(ArrayView{T})"/> method instance to
        /// invoke.
        /// </summary>
        /// <param name="elementType">The array element type.</param>
        /// <returns>The typed method instance.</returns>
        internal static MethodInfo GetClearMethod(Type elementType) =>
            ClearMethod.MakeGenericMethod(elementType);

        /// <summary>
        /// Clears a chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of local memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Clear<T>(ArrayView<T> view)
            where T : unmanaged
        {
            for (int i = 0, e = view.IntLength; i < e; ++i)
                view[i] = default;
        }

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
