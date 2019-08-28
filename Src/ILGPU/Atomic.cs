// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
            where T : struct
        {
            /// <summary>
            /// Realizes an atomic compare-exchange operation.
            /// </summary>
            /// <param name="target">The target location.</param>
            /// <param name="compare">The expected comparison value.</param>
            /// <param name="value">The target value.</param>
            /// <returns>The old value.</returns>
            T CompareExchange(ref T target, T compare, T value);
        }

        /// <summary>
        /// Represents the base interface for atomic binary operations.
        /// </summary>
        /// <typeparam name="T">The parameter type of the atomic operation.</typeparam>
        public interface IAtomicOperation<T>
            where T : struct
        {
            /// <summary>
            /// Performs the actual atomic binary operation.
            /// </summary>
            /// <param name="current">The current value at the target memory location.</param>
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
        /// Atommically adds the given value and the value at the target location
        /// and returns the old value.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index Add(ref Index target, Index value)
        {
            return Add(ref Unsafe.As<Index, int>(ref target), value);
        }

        #endregion

        #region Exchange

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.Exchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Exchange(ref uint target, uint value)
        {
            return (uint)Exchange(ref Unsafe.As<uint, int>(ref target), (int)value);
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.Exchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Exchange(ref ulong target, ulong value)
        {
            return (ulong)Exchange(ref Unsafe.As<ulong, long>(ref target), (long)value);
        }

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
                ref Unsafe.As<float, int>(ref target),
                Unsafe.As<float, int>(ref value));
            return Unsafe.As<int, float>(ref result);
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
                ref Unsafe.As<double, long>(ref target),
                Unsafe.As<double, long>(ref value));
            return Unsafe.As<long, double>(ref result);
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index Exchange(ref Index target, Index value)
        {
            return Exchange(ref Unsafe.As<Index, int>(ref target), value);
        }

        #endregion

        #region Compare & Exchange

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [AtomicIntrinsic(AtomicIntrinsicKind.CompareExchange, AtomicFlags.Unsigned)]
        public static uint CompareExchange(ref uint target, uint compare, uint value)
        {
            return (uint)CompareExchange(
                ref Unsafe.As<uint, int>(ref target),
                (int)compare,
                (int)value);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.CompareExchange, AtomicFlags.Unsigned)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CompareExchange(ref ulong target, ulong compare, ulong value)
        {
            return (ulong)CompareExchange(
                ref Unsafe.As<ulong, long>(ref target),
                (long)compare,
                (long)value);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CompareExchange(ref float target, float compare, float value)
        {
            var result = CompareExchange(
                ref Unsafe.As<float, int>(ref target),
                Unsafe.As<float, int>(ref compare),
                Unsafe.As<float, int>(ref value));
            return Unsafe.As<int, float>(ref result);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CompareExchange(ref double target, double compare, double value)
        {
            var result = CompareExchange(
                ref Unsafe.As<double, long>(ref target),
                Unsafe.As<double, long>(ref compare),
                Unsafe.As<double, long>(ref value));
            return Unsafe.As<long, double>(ref result);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index CompareExchange(ref Index target, Index compare, Index value)
        {
            return CompareExchange(ref Unsafe.As<Index, int>(ref target), compare, value);
        }

        #endregion

        #region Custom

        /// <summary>
        /// Implements a generic pattern to build custom atomic operations.
        /// </summary>
        /// <typeparam name="T">The parameter type of the atomic operation.</typeparam>
        /// <typeparam name="TOperation">The type of the custom atomic operation.</typeparam>
        /// <typeparam name="TCompareExchangeOperation">The type of the custom compare-exchange-operation logic.</typeparam>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <param name="operation">The custom atomic operation.</param>
        /// <param name="compareExchangeOperation">The custom compare-exchange-operation logic.</param>
        /// <returns>The old value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T MakeAtomic<T, TOperation, TCompareExchangeOperation>(
            ref T target,
            T value,
            TOperation operation,
            TCompareExchangeOperation compareExchangeOperation)
            where T : struct, IEquatable<T>
            where TOperation : struct, AtomicOperations.IAtomicOperation<T>
            where TCompareExchangeOperation : struct, AtomicOperations.ICompareExchangeOperation<T>
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
            while (!expected.Equals(current));

            return current;
        }

        #endregion
    }
}
