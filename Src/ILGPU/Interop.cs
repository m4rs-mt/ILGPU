// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Interop.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Contains general interop functions.
    /// </summary>
    public static class Interop
    {
        /// <summary>
        /// Computes the effective address for the given pointer/index combination.
        /// </summary>
        /// <param name="nativePtr">The source pointer.</param>
        /// <param name="index">The element index.</param>
        /// <param name="elementSize">The element size.</param>
        /// <returns>The computed pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref byte ComputeEffectiveAddress(
            ref byte nativePtr,
            Index index,
            int elementSize)
        {
            return ref Unsafe.AddByteOffset(
                ref nativePtr,
                new IntPtr(index * elementSize));
        }

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Matches signature of Marshal.SizeOf")]
        [InteropIntrinsic(InteropIntrinsicKind.SizeOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() => Unsafe.SizeOf<T>();

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T structure) => SizeOf<T>();

        /// <summary>
        /// Computes the unsigned offset of the given field in bytes.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="fieldName">The name of the target field.</param>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type is required for the computation of the field offset")]
        [InteropIntrinsic(InteropIntrinsicKind.OffsetOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OffsetOf<T>(string fieldName) => Marshal.OffsetOf<T>(fieldName).ToInt32();

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static uint FloatAsInt(float value) => Unsafe.As<float, uint>(ref value);

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static ulong FloatAsInt(double value) => Unsafe.As<double, ulong>(ref value);

        /// <summary>
        /// Casts the given int to a float via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The float value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.IntAsFloat)]
        public static float IntAsFloat(uint value) => Unsafe.As<uint, float>(ref value);

        /// <summary>
        /// Casts the given int to a float via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The float value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.IntAsFloat)]
        public static double IntAsFloat(ulong value) => Unsafe.As<ulong, double>(ref value);
    }
}
