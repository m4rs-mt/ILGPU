// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PlatformSupport.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.Util;

/// <summary>
/// Represents general platform support methods.
/// </summary>
static class PlatformSupport
{
    /// <summary>
    /// Returns false to indicate that this system is not supported.
    /// </summary>
    public static bool X86 => false;

    /// <summary>
    /// Returns false to indicate that this system is not supported.
    /// </summary>
    public static bool X64 => false;

    /// <summary>
    /// Returns false to indicate that this system is not supported.
    /// </summary>
    public static bool Arm32 => false;

    /// <summary>
    /// Returns false to indicate that this system is not supported.
    /// </summary>
    public static bool Arm64 => false;

    /// <summary>
    /// Returns false to indicate that this system is not supported.
    /// </summary>
    public static bool Wasm => false;

    /// <summary>
    /// Helpers to map <see cref="System.BitConverter"/> methods to internal methods.
    /// </summary>
    internal static class BitConverter
    {
        /// <summary>
        /// Converts doubles to longs.
        /// </summary>
        public static long DoubleToInt64Bits(double value) =>
            (long)Interop.FloatAsInt(value);

        /// <summary>
        /// Converts longs to doubles.
        /// </summary>
        public static double Int64BitsToDouble(long value) =>
            Interop.IntAsFloat((ulong)value);

        /// <summary>
        /// Converts floats to integers.
        /// </summary>
        public static int SingleToInt32Bits(float value) =>
            (int)Interop.FloatAsInt(value);

        /// <summary>
        /// Converts integers to floats.
        /// </summary>
        public static float Int32BitsToSingle(int value) =>
            Interop.IntAsFloat((uint)value);
    }

    /// <summary>
    /// Helpers to map <see cref="System.Threading.Interlocked"/> methods to internal
    /// methods.
    /// </summary>
    internal static class Interlocked
    {
        /// <summary>
        /// Atomically decrements values.
        /// </summary>
        public static int Decrement(ref int value) => Atomic.Add(ref value, -1);

        /// <summary>
        /// Atomically decrements values.
        /// </summary>
        public static long Decrement(ref long value) => Atomic.Add(ref value, -1);

        /// <summary>
        /// Atomically decrements values.
        /// </summary>
        public static uint Decrement(ref uint value) =>
            (uint)Atomic.Add(ref Unsafe.As<uint, int>(ref value), -1);

        /// <summary>
        /// Atomically decrements values.
        /// </summary>
        public static ulong Decrement(ref ulong value) =>
            (ulong)Atomic.Add(ref Unsafe.As<ulong, long>(ref value), -1);

        /// <summary>
        /// Atomically increments values.
        /// </summary>
        public static int Increment(ref int value) => Atomic.Add(ref value, 1);

        /// <summary>
        /// Atomically decrements values.
        /// </summary>
        public static long Increment(ref long value) => Atomic.Add(ref value, 1);

        /// <summary>
        /// Atomically increments values.
        /// </summary>
        public static uint Increment(ref uint value) =>
            (uint)Atomic.Add(ref Unsafe.As<uint, int>(ref value), 1);

        /// <summary>
        /// Atomically increments values.
        /// </summary>
        public static ulong Increment(ref ulong value) =>
            (ulong)Atomic.Add(ref Unsafe.As<ulong, long>(ref value), 1);
    }
}
