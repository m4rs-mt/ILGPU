// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    namespace AtomicOperations
    {
        /// <summary>
        /// Represents the base interface for compare-exchange operations.
        /// </summary>
        /// <typeparam name="T">The type of the compare-exchange operation.</typeparam>
        public interface ICompareExchangeOperation<T>
            where T : unmanaged
        {
            /// <summary>
            /// Realizes an atomic compare-exchange operation.
            /// </summary>
            /// <param name="target">The target location.</param>
            /// <param name="compare">The expected comparison value.</param>
            /// <param name="value">The target value.</param>
            /// <returns>The old value.</returns>
            T CompareExchange(ref T target, T compare, T value);

            /// <summary>
            /// Returns true if both operands represent the same value.
            /// </summary>
            /// <param name="left">The left operand.</param>
            /// <param name="right">The right operand.</param>
            /// <returns>True, if both operands represent the same value.</returns>
            bool IsSame(T left, T right);
        }

        /// <summary>
        /// Represents the base interface for atomic binary operations.
        /// </summary>
        /// <typeparam name="T">The parameter type of the atomic operation.</typeparam>
        public interface IAtomicOperation<T>
            where T : unmanaged
        {
            /// <summary>
            /// Performs the actual atomic binary operation.
            /// </summary>
            /// <param name="current">
            /// The current value at the target memory location.
            /// </param>
            /// <param name="value">The involved external value.</param>
            /// <returns>The result of the binary operation.</returns>
            T Operation(T current, T value);
        }
    }

    /// <summary>
    /// Contains atomic functions that are supported on devices.
    /// </summary>
    public static partial class Atomic
    {
        #region Add

        /// <summary>
        /// Atomically adds the given value and the value at the target location
        /// and returns the old value.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index1D Add(ref Index1D target, Index1D value) =>
            Add(ref Unsafe.As<Index1D, int>(ref target), value);

        #endregion

        #region Exchange

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.Exchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Exchange(ref uint target, uint value) =>
            (uint)Exchange(ref Unsafe.As<uint, int>(ref target), (int)value);

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.Exchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Exchange(ref ulong target, ulong value) =>
            (ulong)Exchange(ref Unsafe.As<ulong, long>(ref target), (long)value);

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exchange(ref float target, float value)
        {
            var result = Exchange(
                ref Unsafe.As<float, uint>(ref target),
                Interop.FloatAsInt(value));
            return Interop.IntAsFloat(result);
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exchange(ref double target, double value)
        {
            var result = Exchange(
                ref Unsafe.As<double, ulong>(ref target),
                Interop.FloatAsInt(value));
            return Interop.IntAsFloat(result);
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index1D Exchange(ref Index1D target, Index1D value) =>
            Exchange(ref Unsafe.As<Index1D, int>(ref target), value);

        #endregion

        #region Compare & Exchange

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [AtomicIntrinsic(AtomicIntrinsicKind.CompareExchange, AtomicFlags.Unsigned)]
        public static uint CompareExchange(
            ref uint target,
            uint compare,
            uint value) =>
            (uint)CompareExchange(
                ref Unsafe.As<uint, int>(ref target),
                (int)compare,
                (int)value);

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.CompareExchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CompareExchange(
            ref ulong target,
            ulong compare,
            ulong value) =>
            (ulong)CompareExchange(
                ref Unsafe.As<ulong, long>(ref target),
                (long)compare,
                (long)value);


        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half CompareExchange(
            ref Half target,
            Half compare,
            Half value)
        {
            var result = CompareExchange(
                ref Unsafe.As<Half, uint>(ref target),
                Interop.FloatAsInt(compare),
                Interop.FloatAsInt(value));
            return (Half) Interop.IntAsFloat(result);
        }



        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CompareExchange(
            ref float target,
            float compare,
            float value)
        {
            var result = CompareExchange(
                ref Unsafe.As<float, uint>(ref target),
                Interop.FloatAsInt(compare),
                Interop.FloatAsInt(value));
            return Interop.IntAsFloat(result);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CompareExchange(
            ref double target,
            double compare,
            double value)
        {
            var result = CompareExchange(
                ref Unsafe.As<double, ulong>(ref target),
                Interop.FloatAsInt(compare),
                Interop.FloatAsInt(value));
            return Interop.IntAsFloat(result);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index1D CompareExchange(
            ref Index1D target,
            Index1D compare,
            Index1D value) =>
            CompareExchange(
                ref Unsafe.As<Index1D, int>(ref target),
                compare,
                value);

        #endregion

        #region Custom

        /// <summary>
        /// Implements a generic pattern to build custom atomic operations.
        /// </summary>
        /// <typeparam name="T">The parameter type of the atomic operation.</typeparam>
        /// <typeparam name="TOperation">
        /// The type of the custom atomic operation.
        /// </typeparam>
        /// <typeparam name="TCompareExchangeOperation">
        /// The type of the custom compare-exchange-operation logic.
        /// </typeparam>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <param name="operation">The custom atomic operation.</param>
        /// <param name="compareExchangeOperation">
        /// The custom compare-exchange-operation logic.
        /// </param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T MakeAtomic<T, TOperation, TCompareExchangeOperation>(
            ref T target,
            T value,
            TOperation operation,
            TCompareExchangeOperation compareExchangeOperation)
            where T : unmanaged
            where TOperation : struct, AtomicOperations.IAtomicOperation<T>
            where TCompareExchangeOperation :
                struct,
            AtomicOperations.ICompareExchangeOperation<T>
        {
            T current = target;
            T expected;

            do
            {
                expected = current;
                var newValue = operation.Operation(current, value);
                current = compareExchangeOperation.CompareExchange(
                    ref target,
                    expected,
                    newValue);
            }
            while (!compareExchangeOperation.IsSame(expected, current));

            return current;
        }

        #endregion
    }
}
