// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Represents an atomic fusion operation.
/// </summary>
/// <param name="current">The old value.</param>
/// <param name="value">The value to apply.</param>
/// <returns>The new value.</returns>
public delegate T MakeAtomicOperation<T>(T current, T value);

/// <summary>
/// Represents an atomic compare-exchange operation.
/// </summary>
/// <param name="target">The target location.</param>
/// <param name="compare">The expected comparison value.</param>
/// <param name="value">The target value.</param>
/// <returns>The old value.</returns>
public delegate T CompareExchangeOperation<T>(ref T target, T compare, T value);

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
    [AtomicIntrinsic]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [AtomicIntrinsic]
    public static uint Exchange(ref uint target, uint value) =>
        (uint)Exchange(ref Unsafe.As<uint, int>(ref target), (int)value);

    /// <summary>
    /// Represents an atomic exchange operation.
    /// </summary>
    /// <param name="target">The target location.</param>
    /// <param name="value">The target value.</param>
    /// <returns>The old value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [AtomicIntrinsic]
    public static ulong Exchange(ref ulong target, ulong value) =>
        (ulong)Exchange(ref Unsafe.As<ulong, long>(ref target), (long)value);

    /// <summary>
    /// Represents an atomic exchange operation.
    /// </summary>
    /// <param name="target">The target location.</param>
    /// <param name="value">The target value.</param>
    /// <returns>The old value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    [AtomicIntrinsic]
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
    /// <param name="target">The target location.</param>
    /// <param name="value">The target value.</param>
    /// <param name="operation">The custom atomic operation.</param>
    /// <param name="compareExchangeOperation">
    /// The custom compare-exchange-operation logic.
    /// </param>
    /// <returns>The old value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T MakeAtomic<T>(
        ref T target,
        T value,
        MakeAtomicOperation<T> operation,
        CompareExchangeOperation<T> compareExchangeOperation)
        where T : unmanaged, IEquatable<T>
    {
        T current = target;
        T expected;

        do
        {
            expected = current;
            var newValue = operation(current, value);
            current = compareExchangeOperation(ref target, expected, newValue);
        }
        while (!expected.Equals(current));

        return current;
    }

    #endregion
}
