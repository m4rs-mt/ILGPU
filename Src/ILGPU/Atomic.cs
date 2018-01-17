// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.Intrinsic;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

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
            T CompareExchange(VariableView<T> target, T compare, T value);
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
        [AtomicIntrinsic(AtomicIntrinsicKind.AddF32)]
        public static float Add(VariableView<float> target, float value)
        {
            return MakeAtomic(target, value, new AddFloat(), new AtomicOperations.CompareExchangeFloat());
        }

        /// <summary>
        /// Atommically adds the given value and the value at the target location
        /// and returns the old value.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        public static double Add(VariableView<double> target, double value)
        {
            return MakeAtomic(target, value, new AddDouble(), new AtomicOperations.CompareExchangeDouble());
        }

        /// <summary>
        /// Atommically adds the given value and the value at the target location
        /// and returns the old value.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        public static Index Add(VariableView<Index> target, Index value)
        {
            return Add(target.Cast<int>(), value);
        }

        #endregion

        #region Sub

        /// <summary>
        /// Atommically subtracts the given value and the value at the target location
        /// and returns the old value.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The value to subtract.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.Sub)]
        public static Index Sub(VariableView<Index> target, Index value)
        {
            return Sub(target.Cast<int>(), value);
        }

        #endregion

        #region Increment

        struct IncUInt32 : AtomicOperations.IAtomicOperation<uint>
        {
            public uint Operation(uint old, uint value)
            {
                if (old >= value)
                    return 0;
                return old + 1;
            }
        }

        /// <summary>
        /// Atomically increments the target location by 1 and computes ((old >= value) ? 0 : (old+1)).
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The maximum value.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.IncU32)]
        public static uint Increment(VariableView<uint> target, uint value)
        {
            return MakeAtomic(target, value, new IncUInt32(), new AtomicOperations.CompareExchangeUInt32());
        }

        #endregion

        #region Decrement

        struct DecUInt32 : AtomicOperations.IAtomicOperation<uint>
        {
            public uint Operation(uint current, uint value)
            {
                if (current == 0 | current > value)
                    return value;
                return current - 1;
            }
        }

        /// <summary>
        /// Atomically decrements the target location by 1 and computes ((old == 0) | (old > val)) ? val : (old-1).
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The maximum value.</param>
        /// <returns>The old value that was stored at the target location.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.DecU32)]
        public static uint Decrement(VariableView<uint> target, uint value)
        {
            return MakeAtomic(target, value, new DecUInt32(), new AtomicOperations.CompareExchangeUInt32());
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
        public static unsafe uint Exchange(VariableView<uint> target, uint value)
        {
            var result = Exchange(target.Cast<int>(), *(int*)&value);
            return *(uint*)&result;
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        public static unsafe ulong Exchange(VariableView<ulong> target, ulong value)
        {
            var result = Exchange(target.Cast<long>(), *(long*)&value);
            return *(ulong*)&result;
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.Xch)]
        public static unsafe IntPtr Exchange(VariableView<IntPtr> target, IntPtr value)
        {
            return Interlocked.Exchange(ref Unsafe.AsRef<IntPtr>(target.Pointer.ToPointer()), value);
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        public static unsafe float Exchange(VariableView<float> target, float value)
        {
            var result = Exchange(target.Cast<int>(), *(int*)&value);
            return *(float*)&result;
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        public static unsafe double Exchange(VariableView<double> target, double value)
        {
            var result = Exchange(target.Cast<long>(), *(long*)&value);
            return *(double*)&result;
        }

        /// <summary>
        /// Represents an atomic exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        public static Index Exchange(VariableView<Index> target, Index value)
        {
            return Exchange(target.Cast<int>(), value);
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
        [AtomicIntrinsic(AtomicIntrinsicKind.CmpXch)]
        public static unsafe uint CompareExchange(VariableView<uint> target, uint compare, uint value)
        {
            var result = CompareExchange(target.Cast<int>(), *(int*)&compare, *(int*)&value);
            return *(uint*)&result;
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [CLSCompliant(false)]
        [AtomicIntrinsic(AtomicIntrinsicKind.CmpXch)]
        public static unsafe ulong CompareExchange(VariableView<ulong> target, ulong compare, ulong value)
        {
            var result = CompareExchange(target.Cast<long>(), *(long*)&compare, *(long*)&value);
            return *(ulong*)&result;
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        [AtomicIntrinsic(AtomicIntrinsicKind.CmpXch)]
        public static unsafe IntPtr CompareExchange(VariableView<IntPtr> target, IntPtr compare, IntPtr value)
        {
            return Interlocked.CompareExchange(ref Unsafe.AsRef<IntPtr>(target.Pointer.ToPointer()), value, compare);
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        public static unsafe float CompareExchange(VariableView<float> target, float compare, float value)
        {
            var result = CompareExchange(target.Cast<int>(), *(int*)&compare, *(int*)&value);
            return *(float*)&result;
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The target value.</param>
        /// <returns>The old value.</returns>
        public static unsafe double CompareExchange(VariableView<double> target, double compare, double value)
        {
            var result = CompareExchange(target.Cast<long>(), *(long*)&compare, *(long*)&value);
            return *(double*)&result;
        }

        /// <summary>
        /// Represents an atomic compare-exchange operation.
        /// </summary>
        /// <param name="target">the target location.</param>
        /// <param name="compare">The expected comparison value.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The old value.</returns>
        public static Index CompareExchange(VariableView<Index> target, Index compare, Index value)
        {
            return CompareExchange(target.Cast<int>(), compare, value);
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
            VariableView<T> target,
            T value,
            TOperation operation,
            TCompareExchangeOperation compareExchangeOperation)
            where T : struct, IEquatable<T>
            where TOperation : struct, AtomicOperations.IAtomicOperation<T>
            where TCompareExchangeOperation : struct, AtomicOperations.ICompareExchangeOperation<T>
        {
            T current = target.Value;
            T expected;

            do
            {
                expected = current;
                var newValue = operation.Operation(current, value);
                current = compareExchangeOperation.CompareExchange(
                    target,
                    expected,
                    newValue);
            }
            while (!expected.Equals(current));

            return current;
        }

        #endregion
    }
}
