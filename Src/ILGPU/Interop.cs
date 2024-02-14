// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Interop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Util;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Contains general interop functions.
    /// </summary>
    public static partial class Interop
    {
        #region Unsafe

        /// <summary>
        /// Returns an aligned offset that has to be added to the given pointer in order
        /// to compute a new pointer value that is aligned according to the given
        /// alignment specification in bytes.
        /// </summary>
        /// <param name="ptr">The raw integer pointer value.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The pointer offset in bytes to add to the given pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeAlignmentOffset(long ptr, int alignmentInBytes)
        {
            // We can safely cast the pointer value to a 32-bit integer here since the
            // alignment information refers to the lower-most bits only
            int baseOffset = (int)ptr & (alignmentInBytes - 1);
            return Utilities.Select(baseOffset == 0, 0, alignmentInBytes - baseOffset);
        }

        /// <summary>
        /// Returns a properly aligned pointer for the given alignment in bytes.
        /// </summary>
        /// <param name="ptr">The raw integer pointer value.</param>
        /// <param name="length">The maximum buffer length in bytes.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The aligned pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Align(long ptr, long length, int alignmentInBytes)
        {
            long offset =
                IntrinsicMath.Min(length,
                ComputeAlignmentOffset(ptr, alignmentInBytes));
            return ptr + offset;
        }

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
            long index,
            int elementSize) =>
            ref Unsafe.AddByteOffset(
                ref nativePtr,
                new IntPtr(index * elementSize));

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [InteropIntrinsic(InteropIntrinsicKind.SizeOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>()
            where T : unmanaged => Unsafe.SizeOf<T>();

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T structure)
            where T : unmanaged => SizeOf<T>();

        /// <summary>
        /// Computes the size of the given type.
        /// </summary>
        /// <param name="type">The target type</param>
        /// <remarks>Only supports unmanaged types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(Type type) =>
            (int)InteropSizeOfMethod
            .MakeGenericMethod(type)
            .Invoke(null, null)
            .AsNotNull();

        private static readonly MethodInfo InteropSizeOfMethod =
            typeof(Interop).GetMethod(
                nameof(SizeOf),
                Type.EmptyTypes,
                null)
            .ThrowIfNull();

        /// <summary>
        /// Computes number of elements of type <typeparamref name="TFirst"/>
        /// that are required to store a type <typeparamref name="TSecond"/> in
        /// unmanaged memory.
        /// </summary>
        /// <typeparam name="TFirst">
        /// The type that should represent type <typeparamref name="TSecond"/>.
        /// </typeparam>
        /// <typeparam name="TSecond">
        /// The base type that should be represented with <typeparamref name="TFirst"/>.
        /// </typeparam>
        /// <param name="numSecondElements">
        /// The number of <typeparamref name="TSecond"/> elements to be stored.
        /// </param>
        /// <returns>
        /// The number of required <typeparamref name="TFirst"/> instances to store
        /// <paramref name="numSecondElements"/>
        /// instances of type <typeparamref name="TSecond"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ComputeRelativeSizeOf<TFirst, TSecond>(long numSecondElements)
            where TFirst : unmanaged
            where TSecond : unmanaged
        {
            if (numSecondElements < 1)
                throw new ArgumentOutOfRangeException(nameof(numSecondElements));

            var firstSize = SizeOf<TFirst>();
            var secondSize = SizeOf<TSecond>();

            var relativeSize = IntrinsicMath.DivRoundUp(
                secondSize * numSecondElements,
                firstSize);
            return relativeSize;
        }

        /// <summary>
        /// Computes the unsigned offset of the given field in bytes.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="fieldName">The name of the target field.</param>
        [InteropIntrinsic(InteropIntrinsicKind.OffsetOf)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OffsetOf<T>(string fieldName)
            where T : unmanaged =>
            Marshal.OffsetOf<T>(fieldName).ToInt32();

        #endregion

        #region Float/Int Casts

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static ushort FloatAsInt(BFloat16 value) =>
            value.RawValue;

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static ushort FloatAsInt(Half value) =>
            value.RawValue;

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static uint FloatAsInt(float value) =>
            Unsafe.As<float, uint>(ref value);

        /// <summary>
        /// Casts the given float to an int via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.FloatAsInt)]
        public static ulong FloatAsInt(double value) =>
            Unsafe.As<double, ulong>(ref value);

        /// <summary>
        /// Casts the given int to a float via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.IntAsFloat)]
        public static Half IntAsFloat(ushort value) =>
            new Half(value);



        /// <summary>
        /// Casts the given int to a float via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.IntAsFloat)]
        public static float IntAsFloat(uint value) =>
            Unsafe.As<uint, float>(ref value);

        /// <summary>
        /// Casts the given int to a float via a reinterpret cast.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InteropIntrinsic(InteropIntrinsicKind.IntAsFloat)]
        public static double IntAsFloat(ulong value) =>
            Unsafe.As<ulong, double>(ref value);

        #endregion

        #region Write/WriteLine

        /// <summary>
        /// Ensures that the given format is not null and has a proper line ending.
        /// </summary>
        /// <param name="format">The format.</param>
        internal static string GetWriteLineFormat(string format)
        {
            format ??= string.Empty;
            return format.EndsWith(
                Environment.NewLine,
                StringComparison.InvariantCulture)
                ? format
                : format + Environment.NewLine;
        }

        /// <summary>
        /// Writes the given format to the device output.
        /// </summary>
        /// <param name="format">The expression format to write.</param>
        /// <param name="elements">All elements to write in string format.</param>
        internal static void WriteImplementation(
            string format,
            params string[] elements) =>
            Console.Write(format, elements);

        /// <summary>
        /// Writes the given expression to the device output.
        /// </summary>
        /// <param name="expression">The expression to write.</param>
        /// <remarks>
        /// Note that the expression must be a compile-time constant.
        /// </remarks>
        [InteropIntrinsic(InteropIntrinsicKind.Write)]
        public static void Write(string expression) =>
            WriteImplementation(expression ?? string.Empty);

        /// <summary>
        /// Writes the given expression to the device output.
        /// </summary>
        /// <param name="expression">The expression to write.</param>
        /// <remarks>
        /// Note that the expression must be a compile-time constant.
        /// </remarks>
        [InteropIntrinsic(InteropIntrinsicKind.WriteLine)]
        public static void WriteLine(string expression) =>
            WriteImplementation(GetWriteLineFormat(expression));

        #endregion
    }
}
