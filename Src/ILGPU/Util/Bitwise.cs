// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Bitwise.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Util
{
    /// <summary>
    /// Bitwise utility methods.
    /// Used instead of &amp; and | to avoid false-positives from code analysis tools
    /// that suggest they potentially be changed to &amp;&amp; and ||.
    /// </summary>
    public static class Bitwise
    {
        /// <summary>
        /// Performs bitwise operator &amp; on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool And(bool first, bool second) =>
            first & second;

        /// <summary>
        /// Performs bitwise operator &amp; on three values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool And(bool first, bool second, bool third) =>
            first & second & third;

        /// <summary>
        /// Performs bitwise operator &amp; on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int And(int first, int second) =>
            first & second;

        /// <summary>
        /// Performs bitwise operator &amp; on two values.
        /// </summary>\
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint And(uint first, uint second) =>
            first & second;

        /// <summary>
        /// Performs bitwise operator &amp; on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long And(long first, long second) =>
            first & second;

        /// <summary>
        /// Performs bitwise operator &amp; on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong And(ulong first, ulong second) =>
            first & second;

        /// <summary>
        /// Performs bitwise operator | on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Or(bool first, bool second) =>
            first | second;

        /// <summary>
        /// Performs bitwise operator | on three values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Or(bool first, bool second, bool third) =>
            first | second | third;

        /// <summary>
        /// Performs bitwise operator | on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Or(int first, int second) =>
            first | second;

        /// <summary>
        /// Performs bitwise operator | on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Or(uint first, uint second) =>
            first | second;

        /// <summary>
        /// Performs bitwise operator | on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Or(long first, long second) =>
            first | second;

        /// <summary>
        /// Performs bitwise operator | on two values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Or(ulong first, ulong second) =>
            first | second;
    }
}
