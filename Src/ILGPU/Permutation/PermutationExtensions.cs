// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PermutationExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.RadixSort;
using ILGPU.Random;
using System.Runtime.CompilerServices;

namespace ILGPU;

partial class Group
{
    /// <summary>
    /// Selects the random bits of a permutation key above the 14 bits that hold
    /// the index of a thread in its group or warp.
    /// </summary>
    internal const int RandomKeyMask = 0x7fffc000;

    /// <summary>
    /// Permutes the given value within a group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(T value)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        var randomScope = GetRandom<TRandomProvider>();
        var result = Permute(value, ref randomScope);
        randomScope.Dispose();
        return result;
    }

    /// <summary>
    /// Permutes the given value within a group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(
        T value,
        ref TRandomProvider random)
        where T : unmanaged
        where TRandomProvider : struct, IRandomProvider
    {
        // Random bits above the thread index make the keys unique
        var threadValue = (random.Next() & RandomKeyMask) + Index;
        return Permute(value, threadValue);
    }

    /// <summary>
    /// Permutes the given value within a group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="threadValue">The random number for this thread.</param>
    /// <returns>A permuted value from another random group index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Permute<T>(T value, int threadValue) where T : unmanaged
    {
        // Get shared memory and populate it
        var sharedMemory = GetSharedMemoryPerThread<T>();
        sharedMemory[Index] = value;
        Barrier();

        // Sort all lanes appropriately and read the value of the thread whose
        // index is in the low bits of the sorted key
        int index = RadixSort<int, AscendingInt32>(threadValue);
        var result = sharedMemory[index & ~RandomKeyMask];

        // The buffer of this call site is the buffer of every call of this
        // method in a kernel: wait until all threads have read it before a
        // later call stores to it
        Barrier();
        return result;
    }
}

partial class Warp
{
    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(T value)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        var randomScope = GetRandom<TRandomProvider>();
        var result = Permute(value, ref randomScope);
        randomScope.Dispose();
        return result;
    }

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(
        T value,
        ref TRandomProvider random)
        where T : unmanaged
        where TRandomProvider : struct, IRandomProvider
    {
        // Random bits above the lane index make the keys unique
        var laneValue = (random.Next() & Group.RandomKeyMask) + LaneIndex;
        return Permute(value, laneValue);
    }

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="laneValue">The random number for this lane.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Permute<T>(T value, int laneValue) where T : unmanaged
    {
        int lane = RadixSort<int, AscendingInt32>(laneValue);
        var newLane = lane & Mask;
        var newElement = value;
        for (int i = 0; i < Dimension; i++)
        {
            var targetLane = Shuffle(newLane, i);
            var retrievedElement = Shuffle(value, i);
            if (targetLane == LaneIndex)
                newElement = retrievedElement;
        }
        return newElement;
    }
}
