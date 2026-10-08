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

    /// <summary>
    /// Broadcasts a value from one lane to all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] BroadcastFrom<T>(ReadOnlySpan<T> source, int originLane)
    {
        var data = new T[source.Length];
        Array.Fill(data, source[originLane]);
        return data;
    }

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
    /// Initializes per-lane pointers into a contiguous byte span.
    /// Each lane gets <paramref name="elementSize"/> bytes of storage.
    /// The span must be at least <c>pointers.Length * elementSize</c>
    /// bytes long and must remain pinned for the lifetime of the pointers.
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

    /// <summary>
    /// Computes per-lane pointers offset by a fixed byte amount from
    /// a source pointer array. Used for struct field access on per-lane
    /// alloca pointers (e.g., accessing field at byte offset 4 in each
    /// lane's struct allocation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PointerOffset(
        Span<nint> result, ReadOnlySpan<nint> pointers,
        int byteOffset)
    {
        for (int i = 0; i < result.Length; i++)
            result[i] = pointers[i] + byteOffset;
    }

    #endregion
}
