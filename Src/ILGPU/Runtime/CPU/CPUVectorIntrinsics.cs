// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUVectorIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// General helper functions for vectorized execution.
/// All methods are aggressively inlined for optimal performance.
/// </summary>
public static class CPUVectorIntrinsics
{
    #region Broadcast Operations

    /// <summary>
    /// Broadcasts a single value to all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Broadcast<T>(T value, int length)
    {
        var data = new T[length];
        Array.Fill(data, value);
        return data;
    }

    /// <summary>
    /// Broadcasts a single value to all lanes of a pre-allocated target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Broadcast<T>(Span<T> target, T value) =>
        target.Fill(value);

    #endregion

    #region Shuffle Operations

    /// <summary>
    /// Shuffles values across lanes (generic shuffle).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Shuffle<T>(ReadOnlySpan<T> source, ReadOnlySpan<int> indices)
    {
        var res = new T[source.Length];
        for (int i = 0; i < res.Length; i++)
        {
            int srcIndex = indices[i];
            if (srcIndex >= 0 && srcIndex < source.Length)
                res[i] = source[srcIndex];
        }
        return res;
    }

    /// <summary>
    /// Shuffle down: each lane receives value from lane (i + delta).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] ShuffleDown<T>(ReadOnlySpan<T> source, int delta)
        where T : struct
    {
        var res = new T[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            int srcIndex = i + delta;
            res[i] = srcIndex < source.Length ? source[srcIndex] : default;
        }
        return res;
    }

    /// <summary>
    /// Shuffle up: each lane receives value from lane (i - delta).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] ShuffleUp<T>(ReadOnlySpan<T> source, int delta)
        where T : struct
    {
        var res = new T[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            int srcIndex = i - delta;
            res[i] = srcIndex >= 0 ? source[srcIndex] : default;
        }
        return res;
    }

    /// <summary>
    /// Shuffle XOR: each lane receives value from lane (i XOR delta).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] ShuffleXor<T>(ReadOnlySpan<T> source, int delta)
        where T : struct
    {
        var res = new T[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            int srcIndex = i ^ delta;
            res[i] = srcIndex < source.Length ? source[srcIndex] : default;
        }
        return res;
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Selects values based on a condition (similar to HLSL's select).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Select<T>(
        ReadOnlySpan<bool> condition,
        ReadOnlySpan<T> trueValues, ReadOnlySpan<T> falseValues)
    {
        var res = new T[condition.Length];
        for (int i = 0; i < condition.Length; i++)
            res[i] = condition[i] ? trueValues[i] : falseValues[i];
        return res;
    }

    /// <summary>
    /// Selects values based on a condition into a pre-allocated target.
    /// Safe when target aliases falseValues (reads element before writing).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Select<T>(
        Span<T> target,
        ReadOnlySpan<bool> condition,
        ReadOnlySpan<T> trueValues,
        ReadOnlySpan<T> falseValues)
    {
        for (int i = 0; i < condition.Length; i++)
            target[i] = condition[i] ? trueValues[i] : falseValues[i];
    }

    /// <summary>
    /// Writes a uniform value into the lanes of a target selected by a mask
    /// and keeps all other lanes unchanged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SelectFill<T>(
        Span<T> target,
        ReadOnlySpan<bool> condition,
        T value)
    {
        for (int i = 0; i < condition.Length; i++)
        {
            if (condition[i])
                target[i] = value;
        }
    }

    /// <summary>
    /// Returns the value of the last active lane, which is the value a
    /// sequential execution of the active lanes leaves behind when every lane
    /// writes its value to the same location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LastActive<T>(ReadOnlySpan<T> values, ReadOnlySpan<bool> mask)
    {
        for (int i = mask.Length - 1; i > 0; i--)
        {
            if (mask[i])
                return values[i];
        }
        return values[0];
    }

    /// <summary>
    /// Checks if all lanes satisfy a condition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < mask.Length; i++)
            if (!mask[i]) return false;
        return true;
    }

    /// <summary>
    /// Checks if any lane satisfies a condition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < mask.Length; i++)
            if (mask[i]) return true;
        return false;
    }

    /// <summary>
    /// Counts the number of true values in a mask.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ReadOnlySpan<bool> mask)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
            if (mask[i]) count++;
        return count;
    }

    #endregion

    #region Mask Operations

    /// <summary>
    /// Creates a bool[] mask with all lanes set to true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] CreateAllTrueMask(int length)
    {
        var data = new bool[length];
        Array.Fill(data, true);
        return data;
    }

    /// <summary>
    /// Creates lane index array [0, 1, 2, ..., width-1].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] CreateLaneIndices(int width)
    {
        var data = new int[width];
        for (int i = 0; i < width; i++)
            data[i] = i;
        return data;
    }

    /// <summary>
    /// Re-initializes a lane index array starting from baseIndex.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitLaneIndices(int[] laneIdx, int baseIndex)
    {
        for (int i = 0; i < laneIdx.Length; i++)
            laneIdx[i] = baseIndex + i;
    }

    /// <summary>
    /// Initializes an active mask: lanes 0..activeLanes-1 are true, rest false.
    /// Used to mask out out-of-bounds SIMD lanes in the last group.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitActiveMask(bool[] mask, int activeLanes)
    {
        for (int i = 0; i < mask.Length; i++)
            mask[i] = i < activeLanes;
    }

    /// <summary>
    /// Copies a mask to a new bool[].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] CopyMask(ReadOnlySpan<bool> source) =>
        source.ToArray();

    /// <summary>
    /// Copies a mask into a target bool[] in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyMask(bool[] target, ReadOnlySpan<bool> source) =>
        source.CopyTo(target);

    /// <summary>
    /// Computes element-wise AND of two masks: result[i] = a[i] &amp; b[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] AndMask(ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        var res = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            res[i] = a[i] & b[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise AND of two masks, writing result into target in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AndMask(bool[] target, ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = a[i] & b[i];
    }

    /// <summary>
    /// Computes element-wise AND-NOT: result[i] = a[i] &amp; !b[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] AndNotMask(ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        var res = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            res[i] = a[i] & !b[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise AND-NOT, writing result into target in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AndNotMask(
        bool[] target, ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = a[i] & !b[i];
    }

    /// <summary>
    /// Computes element-wise OR of two masks: result[i] = a[i] | b[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] OrMask(ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        var res = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            res[i] = a[i] | b[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise OR of two masks, writing result into target in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OrMask(bool[] target, ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = a[i] | b[i];
    }

    /// <summary>
    /// Computes element-wise NOT: result[i] = !a[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] NotMask(ReadOnlySpan<bool> a)
    {
        var res = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            res[i] = !a[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise NOT, writing the result into target in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotMask(bool[] target, ReadOnlySpan<bool> a)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = !a[i];
    }

    /// <summary>
    /// Computes element-wise XOR of two masks, writing the result into target
    /// in-place: target[i] = a[i] ^ b[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void XorMask(bool[] target, ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = a[i] ^ b[i];
    }

    /// <summary>
    /// Computes element-wise equality of two masks, writing the result into
    /// target in-place: target[i] = a[i] == b[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EqualMask(
        bool[] target, ReadOnlySpan<bool> a, ReadOnlySpan<bool> b)
    {
        for (int i = 0; i < a.Length; i++)
            target[i] = a[i] == b[i];
    }

    /// <summary>
    /// Moves a pending block entry mask into the active mask and clears the
    /// pending mask: target[i] = source[i], source[i] = false.
    /// </summary>
    /// <returns>True if any lane of the moved mask is active.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TakeMask(bool[] target, bool[] source)
    {
        bool any = false;
        for (int i = 0; i < target.Length; i++)
        {
            bool active = source[i];
            target[i] = active;
            source[i] = false;
            any |= active;
        }
        return any;
    }

    /// <summary>
    /// Computes the lanes of a mask that take a switch case:
    /// target[i] = mask[i] &amp; values[i] == caseValue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CaseMask<T>(
        bool[] target,
        ReadOnlySpan<bool> mask,
        ReadOnlySpan<T> values,
        T caseValue)
        where T : IEqualityOperators<T, T, bool>
    {
        for (int i = 0; i < target.Length; i++)
            target[i] = mask[i] & values[i] == caseValue;
    }

    /// <summary>
    /// Computes the lanes of a mask that take the default target of a switch
    /// with the given number of cases:
    /// target[i] = mask[i] &amp; (values[i] &lt; 0 | values[i] &gt;= numCases).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DefaultCaseMask<T>(
        bool[] target,
        ReadOnlySpan<bool> mask,
        ReadOnlySpan<T> values,
        T numCases)
        where T : INumber<T>
    {
        for (int i = 0; i < target.Length; i++)
        {
            target[i] = mask[i]
                & (values[i] < T.Zero | values[i] >= numCases);
        }
    }

    /// <summary>
    /// Adds the lanes of a mask to a pending mask: target[i] |= mask[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AccumulateMask(bool[] target, ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < target.Length; i++)
            target[i] |= mask[i];
    }

    #endregion

    #region Predicate Barriers (masked)

    // A vectorized kernel call runs one group of SIMD-width threads in
    // lockstep, so a group or warp predicate barrier combines the predicate
    // across the lanes of the call. Only the active lanes take part: lanes
    // beyond the launch extent and lanes that left the barrier's block on
    // a divergent path do not count, like threads that have exited.

    /// <summary>
    /// Fills <paramref name="target"/> with the number of active lanes whose
    /// predicate is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BarrierPopCount(
        Span<int> target,
        ReadOnlySpan<bool> predicate,
        ReadOnlySpan<bool> mask)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] && predicate[i])
                count++;
        }
        target.Fill(count);
    }

    /// <summary>
    /// Fills <paramref name="target"/> with the number of active lanes for a
    /// predicate that is the same in all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BarrierPopCount(
        Span<int> target,
        bool predicate,
        ReadOnlySpan<bool> mask) =>
        target.Fill(predicate ? PopCount(mask) : 0);

    /// <summary>
    /// Fills <paramref name="target"/> with true if the predicate is true in
    /// all active lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BarrierAnd(
        Span<bool> target,
        ReadOnlySpan<bool> predicate,
        ReadOnlySpan<bool> mask)
    {
        bool result = true;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] && !predicate[i])
            {
                result = false;
                break;
            }
        }
        target.Fill(result);
    }

    /// <summary>
    /// Fills <paramref name="target"/> with true if the predicate is true in
    /// any active lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BarrierOr(
        Span<bool> target,
        ReadOnlySpan<bool> predicate,
        ReadOnlySpan<bool> mask)
    {
        bool result = false;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] && predicate[i])
            {
                result = true;
                break;
            }
        }
        target.Fill(result);
    }

    #endregion

    #region Warp Reduce Operations (masked)

    // The CPU vector backend launches SIMD groups of fixed width. When a
    // launch extent is not a multiple of the group width, the last group
    // runs with only the first N lanes "active" (see activeMask). Warp
    // reduce/scan must respect that mask — otherwise stale values in the
    // tail lanes pollute the reduction.
    //
    // All helpers below return a pre-filled T[] so the vectorized caller
    // can use the result as a per-lane broadcast.

    /// <summary>
    /// Masked all-reduce addition across SIMD lanes.
    /// Inactive lanes contribute the additive identity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceAdd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        T acc = T.AdditiveIdentity;
        for (int i = 0; i < values.Length; i++)
            if (mask[i]) acc += values[i];
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce multiplication across SIMD lanes.
    /// Inactive lanes contribute the multiplicative identity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceMul<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        T acc = T.MultiplicativeIdentity;
        for (int i = 0; i < values.Length; i++)
            if (mask[i]) acc *= values[i];
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce minimum across SIMD lanes.
    /// Inactive lanes are excluded; when no lane is active the first
    /// element is returned (caller won't consume inactive output).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceMin<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IComparisonOperators<T, T, bool>
    {
        T acc = default!;
        bool seen = false;
        for (int i = 0; i < values.Length; i++)
        {
            if (!mask[i]) continue;
            if (!seen || values[i] < acc) { acc = values[i]; seen = true; }
        }
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce maximum across SIMD lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceMax<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IComparisonOperators<T, T, bool>
    {
        T acc = default!;
        bool seen = false;
        for (int i = 0; i < values.Length; i++)
        {
            if (!mask[i]) continue;
            if (!seen || values[i] > acc) { acc = values[i]; seen = true; }
        }
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce bitwise-AND across SIMD lanes.
    /// Inactive lanes contribute all-ones (no-op for AND).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceAnd<T>(
        ReadOnlySpan<T> values, ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T>
    {
        T acc = default!;
        bool seen = false;
        for (int i = 0; i < values.Length; i++)
        {
            if (!mask[i]) continue;
            acc = seen ? (acc & values[i]) : values[i];
            seen = true;
        }
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce bitwise-OR across SIMD lanes.
    /// Inactive lanes contribute zero (no-op for OR).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceOr<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        T acc = T.AdditiveIdentity;
        for (int i = 0; i < values.Length; i++)
            if (mask[i]) acc |= values[i];
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    /// <summary>
    /// Masked all-reduce bitwise-XOR across SIMD lanes.
    /// Inactive lanes contribute zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpReduceXor<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        T acc = T.AdditiveIdentity;
        for (int i = 0; i < values.Length; i++)
            if (mask[i]) acc ^= values[i];
        var result = new T[values.Length];
        Array.Fill(result, acc);
        return result;
    }

    #endregion

    #region Warp Scan Operations (masked, inclusive + exclusive)

    // Scans write the per-lane prefix; inactive lanes get default (0/identity)
    // since their value is not consumed by the calling kernel.

    /// <summary>Masked inclusive prefix sum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveAdd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var result = new T[values.Length];
        T acc = T.AdditiveIdentity;
        for (int i = 0; i < values.Length; i++)
        {
            if (mask[i])
            {
                acc += values[i];
                result[i] = acc;
            }
        }
        return result;
    }

    /// <summary>Masked exclusive prefix sum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveAdd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var result = new T[values.Length];
        T acc = T.AdditiveIdentity;
        for (int i = 0; i < values.Length; i++)
        {
            if (mask[i])
            {
                result[i] = acc;
                acc += values[i];
            }
        }
        return result;
    }

    /// <summary>Masked inclusive prefix product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveMul<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        var result = new T[values.Length];
        T acc = T.MultiplicativeIdentity;
        for (int i = 0; i < values.Length; i++)
        {
            if (mask[i])
            {
                acc *= values[i];
                result[i] = acc;
            }
        }
        return result;
    }

    /// <summary>Masked exclusive prefix product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveMul<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        var result = new T[values.Length];
        T acc = T.MultiplicativeIdentity;
        for (int i = 0; i < values.Length; i++)
        {
            if (mask[i])
            {
                result[i] = acc;
                acc *= values[i];
            }
        }
        return result;
    }

    /// <summary>Masked exclusive prefix sum from the given identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveAdd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IAdditionOperators<T, T, T> =>
        ScanExclusive<T, AddOperation<T>>(values, mask, identity);

    /// <summary>Masked exclusive prefix product from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveMul<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IMultiplyOperators<T, T, T> =>
        ScanExclusive<T, MulOperation<T>>(values, mask, identity);

    /// <summary>Masked inclusive prefix minimum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveMin<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IComparisonOperators<T, T, bool> =>
        ScanInclusive<T, MinOperation<T>>(values, mask);

    /// <summary>Masked exclusive prefix minimum from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveMin<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IComparisonOperators<T, T, bool> =>
        ScanExclusive<T, MinOperation<T>>(values, mask, identity);

    /// <summary>Masked inclusive prefix maximum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveMax<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IComparisonOperators<T, T, bool> =>
        ScanInclusive<T, MaxOperation<T>>(values, mask);

    /// <summary>Masked exclusive prefix maximum from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveMax<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IComparisonOperators<T, T, bool> =>
        ScanExclusive<T, MaxOperation<T>>(values, mask, identity);

    /// <summary>Masked inclusive prefix bitwise-AND.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveAnd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T> =>
        ScanInclusive<T, AndOperation<T>>(values, mask);

    /// <summary>Masked exclusive prefix bitwise-AND from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveAnd<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IBitwiseOperators<T, T, T> =>
        ScanExclusive<T, AndOperation<T>>(values, mask, identity);

    /// <summary>Masked inclusive prefix bitwise-OR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveOr<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T> =>
        ScanInclusive<T, OrOperation<T>>(values, mask);

    /// <summary>Masked exclusive prefix bitwise-OR from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveOr<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IBitwiseOperators<T, T, T> =>
        ScanExclusive<T, OrOperation<T>>(values, mask, identity);

    /// <summary>Masked inclusive prefix bitwise-XOR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanInclusiveXor<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where T : IBitwiseOperators<T, T, T> =>
        ScanInclusive<T, XorOperation<T>>(values, mask);

    /// <summary>Masked exclusive prefix bitwise-XOR from the identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] WarpScanExclusiveXor<T>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where T : IBitwiseOperators<T, T, T> =>
        ScanExclusive<T, XorOperation<T>>(values, mask, identity);

    /// <summary>
    /// A binary operation combining the values of two lanes in a scan.
    /// </summary>
    private interface IScanOperation<T>
    {
        static abstract T Combine(T left, T right);
    }

    private readonly struct AddOperation<T> : IScanOperation<T>
        where T : IAdditionOperators<T, T, T>
    {
        public static T Combine(T left, T right) => left + right;
    }

    private readonly struct MulOperation<T> : IScanOperation<T>
        where T : IMultiplyOperators<T, T, T>
    {
        public static T Combine(T left, T right) => left * right;
    }

    private readonly struct MinOperation<T> : IScanOperation<T>
        where T : IComparisonOperators<T, T, bool>
    {
        public static T Combine(T left, T right) => right < left ? right : left;
    }

    private readonly struct MaxOperation<T> : IScanOperation<T>
        where T : IComparisonOperators<T, T, bool>
    {
        public static T Combine(T left, T right) => right > left ? right : left;
    }

    private readonly struct AndOperation<T> : IScanOperation<T>
        where T : IBitwiseOperators<T, T, T>
    {
        public static T Combine(T left, T right) => left & right;
    }

    private readonly struct OrOperation<T> : IScanOperation<T>
        where T : IBitwiseOperators<T, T, T>
    {
        public static T Combine(T left, T right) => left | right;
    }

    private readonly struct XorOperation<T> : IScanOperation<T>
        where T : IBitwiseOperators<T, T, T>
    {
        public static T Combine(T left, T right) => left ^ right;
    }

    /// <summary>
    /// Masked inclusive scan starting at the first active lane.
    /// </summary>
    private static T[] ScanInclusive<T, TOperation>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
        where TOperation : IScanOperation<T>
    {
        var result = new T[values.Length];
        T acc = default!;
        bool seen = false;
        for (int i = 0; i < values.Length; i++)
        {
            if (!mask[i]) continue;
            acc = seen ? TOperation.Combine(acc, values[i]) : values[i];
            seen = true;
            result[i] = acc;
        }
        return result;
    }

    /// <summary>
    /// Masked exclusive scan starting at the given identity.
    /// </summary>
    private static T[] ScanExclusive<T, TOperation>(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask,
        T identity)
        where TOperation : IScanOperation<T>
    {
        var result = new T[values.Length];
        T acc = identity;
        for (int i = 0; i < values.Length; i++)
        {
            if (!mask[i]) continue;
            result[i] = acc;
            acc = TOperation.Combine(acc, values[i]);
        }
        return result;
    }

    #endregion

    #region Pointer Operations

    /// <summary>
    /// Computes per-lane memory addresses from a <see cref="CPURuntimeView{T}"/>
    /// base pointer and per-lane indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ComputeViewAddresses<T>(
        Span<nint> addresses,
        CPURuntimeView<T> view,
        ReadOnlySpan<int> indices,
        int elementSize,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < addresses.Length; i++)
        {
            if (mask[i])
                addresses[i] = (nint)((byte*)view.Ptr + indices[i] * elementSize);
        }
    }

    /// <summary>
    /// Computes the address of the element at the given per-lane index in the
    /// private array of every active lane of a per-lane view.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ComputeLaneViewAddresses<T>(
        Span<nint> addresses,
        CPURuntimeView<T> view,
        ReadOnlySpan<int> indices,
        int elementSize,
        ReadOnlySpan<bool> mask,
        int laneStride) where T : unmanaged
    {
        for (int i = 0; i < addresses.Length; i++)
        {
            if (mask[i])
            {
                long element = (long)i * laneStride + indices[i];
                addresses[i] = (nint)((byte*)view.Ptr + element * elementSize);
            }
        }
    }

    /// <summary>
    /// Points each lane's pointer at its own element-sized slot of storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void InitAllocaPointers(
        Span<nint> pointers, Span<byte> storage, int elementSize)
    {
        fixed (byte* p = storage)
        {
            for (int i = 0; i < pointers.Length; i++)
                pointers[i] = (nint)(p + i * elementSize);
        }
    }

    /// <summary>
    /// Copies per-lane pointers from source to destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyPointers(
        Span<nint> dest, ReadOnlySpan<nint> source)
    {
        source.CopyTo(dest);
    }

    /// <summary>
    /// Offsets each per-lane pointer by a fixed byte amount.
    /// Used for LoadFieldAddress on vectorized allocas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OffsetPointers(
        Span<nint> dest, ReadOnlySpan<nint> source, int byteOffset)
    {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = source[i] + byteOffset;
    }

    /// <summary>
    /// Offsets each per-lane pointer by a fixed (possibly large) byte amount.
    /// Used for element addresses with uniform offsets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OffsetPointers(
        Span<nint> dest, ReadOnlySpan<nint> source, long byteOffset)
    {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = (nint)(source[i] + byteOffset);
    }

    /// <summary>
    /// Offsets each per-lane pointer by its own number of elements.
    /// Used for element addresses with per-lane offsets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OffsetPointers(
        Span<nint> dest,
        ReadOnlySpan<nint> source,
        ReadOnlySpan<int> offsets,
        int elementSize)
    {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = source[i] + (nint)offsets[i] * elementSize;
    }

    /// <summary>
    /// Offsets each per-lane pointer by its own number of elements.
    /// Used for element addresses with per-lane 64-bit offsets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OffsetPointers(
        Span<nint> dest,
        ReadOnlySpan<nint> source,
        ReadOnlySpan<long> offsets,
        int elementSize)
    {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = (nint)(source[i] + offsets[i] * elementSize);
    }

    /// <summary>
    /// Stores values through per-lane pointers with masking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void PointerStore<T>(
        ReadOnlySpan<nint> pointers, ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < pointers.Length; i++)
        {
            if (mask[i])
                Unsafe.Write((void*)pointers[i], values[i]);
        }
    }

    /// <summary>
    /// Stores a scalar value through all active per-lane pointers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void PointerStoreFill<T>(
        ReadOnlySpan<nint> pointers, T value,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < pointers.Length; i++)
        {
            if (mask[i])
                Unsafe.Write((void*)pointers[i], value);
        }
    }

    /// <summary>
    /// Loads values from per-lane pointers with masking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void PointerLoad<T>(
        Span<T> result, ReadOnlySpan<nint> pointers,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < result.Length; i++)
            result[i] = mask[i] ? Unsafe.Read<T>((void*)pointers[i]) : default;
    }

    #endregion

    #region Per-Lane View Operations

    // Views that differ per lane are stored as one view per SIMD lane. Element
    // accesses go through the view of each active lane; inactive lanes are
    // neither read nor written.

    /// <summary>
    /// Writes the view of the private array of every lane of a local array
    /// into a per-lane view buffer: lane i owns the elements
    /// [i * laneStride, i * laneStride + view.Length) of the underlying memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void LaneViews<T>(
        Span<CPURuntimeView<T>> views,
        CPURuntimeView<T> view,
        int laneStride) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
            views[i] = new(view.Ptr + (long)i * laneStride, view.Length);
    }

    /// <summary>
    /// Loads the element at a per-lane index from the view of every active
    /// lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewGatherLoad<T>(
        Span<T> target,
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<int> indices,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (mask[i])
                target[i] = views[i].Ptr[indices[i]];
        }
    }

    /// <inheritdoc cref="ViewGatherLoad{T}(Span{T},
    /// ReadOnlySpan{CPURuntimeView{T}}, ReadOnlySpan{int}, ReadOnlySpan{bool})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewGatherLoad<T>(
        Span<T> target,
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<long> indices,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (mask[i])
                target[i] = views[i].Ptr[indices[i]];
        }
    }

    /// <summary>
    /// Loads the element at a uniform index from the view of every active
    /// lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewGatherLoad<T>(
        Span<T> target,
        ReadOnlySpan<CPURuntimeView<T>> views,
        long index,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (mask[i])
                target[i] = views[i].Ptr[index];
        }
    }

    /// <summary>
    /// Stores one value per active lane at a per-lane index into the view of
    /// the lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterStore<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<int> indices,
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[indices[i]] = values[i];
        }
    }

    /// <inheritdoc cref="ViewScatterStore{T}(ReadOnlySpan{CPURuntimeView{T}},
    /// ReadOnlySpan{int}, ReadOnlySpan{T}, ReadOnlySpan{bool})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterStore<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<long> indices,
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[indices[i]] = values[i];
        }
    }

    /// <summary>
    /// Stores one value per active lane at a uniform index into the view of
    /// the lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterStore<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        long index,
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[index] = values[i];
        }
    }

    /// <summary>
    /// Stores a uniform value at a per-lane index into the view of every
    /// active lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterFill<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<int> indices,
        T value,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[indices[i]] = value;
        }
    }

    /// <inheritdoc cref="ViewScatterFill{T}(ReadOnlySpan{CPURuntimeView{T}},
    /// ReadOnlySpan{int}, T, ReadOnlySpan{bool})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterFill<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<long> indices,
        T value,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[indices[i]] = value;
        }
    }

    /// <summary>
    /// Stores a uniform value at a uniform index into the view of every
    /// active lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ViewScatterFill<T>(
        ReadOnlySpan<CPURuntimeView<T>> views,
        long index,
        T value,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (mask[i])
                views[i].Ptr[index] = value;
        }
    }

    /// <summary>
    /// Computes the address of the element at a per-lane index in the view of
    /// every active lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ComputeViewAddresses<T>(
        Span<nint> addresses,
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<int> indices,
        int elementSize,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < addresses.Length; i++)
        {
            if (mask[i])
            {
                addresses[i] = (nint)(
                    (byte*)views[i].Ptr + (long)indices[i] * elementSize);
            }
        }
    }

    /// <inheritdoc cref="ComputeViewAddresses{T}(Span{nint},
    /// ReadOnlySpan{CPURuntimeView{T}}, ReadOnlySpan{int}, int,
    /// ReadOnlySpan{bool})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ComputeViewAddresses<T>(
        Span<nint> addresses,
        ReadOnlySpan<CPURuntimeView<T>> views,
        ReadOnlySpan<long> indices,
        int elementSize,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < addresses.Length; i++)
        {
            if (mask[i])
            {
                addresses[i] = (nint)(
                    (byte*)views[i].Ptr + indices[i] * elementSize);
            }
        }
    }

    /// <summary>
    /// Computes the address of the element at a uniform index in the view of
    /// every active lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ComputeViewAddresses<T>(
        Span<nint> addresses,
        ReadOnlySpan<CPURuntimeView<T>> views,
        long index,
        int elementSize,
        ReadOnlySpan<bool> mask) where T : unmanaged
    {
        for (int i = 0; i < addresses.Length; i++)
        {
            if (mask[i])
                addresses[i] = (nint)((byte*)views[i].Ptr + index * elementSize);
        }
    }

    #endregion
}
