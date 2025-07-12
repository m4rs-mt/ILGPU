// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelSize.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.InteropServices;

namespace ILGPU;

/// <summary>
/// Represents a kernel dimension.
/// </summary>
/// <param name="GridSize">The number of groups to launch.</param>
/// <param name="GroupSize">The number of threads per group.</param>
[Serializable]
[StructLayout(LayoutKind.Sequential, Size = 16)]
public readonly record struct KernelSize(long GridSize, int GroupSize) :
    IComparable<KernelSize>
{
    /// <summary>
    /// Returns the total number of threads to launch.
    /// </summary>
    public long NumThreads => GridSize * GroupSize;

    /// <summary>
    /// Compares this kernel size to the given one by number of threads.
    /// </summary>
    /// <param name="other">The other kernel size to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(KernelSize other) => other.NumThreads.CompareTo(NumThreads);

    /// <summary>
    /// Returns true if the given kernel index is in bounds of this kernel size.
    /// </summary>
    /// <param name="index">The index to test.</param>
    /// <returns>True if the given kernel index is in bounds.</returns>
    public bool IsInBounds(KernelIndex index) =>
        Bitwise.And(index.GridIndex < GridSize, index.GroupIndex < GroupSize);

    /// <summary>
    /// Computes number of groups to process the given number of elements.
    /// </summary>
    /// <param name="numElements">The number of elements to process.</param>
    /// <returns>
    /// The number of groups or zero in case of an invalid number of elements.
    /// </returns>
    public long ComputeNumGroups(long numElements) =>
        Math.Max(XMath.DivRoundUp(numElements, GroupSize), 0L);

    /// <summary>
    /// Validates this kernel dimension structure and throws an
    /// <see cref="ArgumentOutOfRangeException"/> in case of invalid launch dimensions.
    /// </summary>
    public void Validate()
    {
        if (GridSize < 1)
            throw new ArgumentOutOfRangeException(nameof(GridSize));
        if (GroupSize < 1)
            throw new ArgumentOutOfRangeException(nameof(GroupSize));
    }

    /// <summary>
    /// Returns a string representation of this kernel size instance.
    /// </summary>
    public override string ToString() => $"{GridSize}x{GroupSize}";

    /// <summary>
    /// Compares the left against the right kernel size comparing by number of threads.
    /// </summary>
    /// <param name="left">The left kernel size to compare.</param>
    /// <param name="right">The right kernel size to compare.</param>
    /// <returns>True if the left size is greater than the right size.</returns>
    public static bool operator >(KernelSize left, KernelSize right) =>
        left.CompareTo(right) > 0;

    /// <summary>
    /// Compares the left against the right kernel size comparing by number of threads.
    /// </summary>
    /// <param name="left">The left kernel size to compare.</param>
    /// <param name="right">The right kernel size to compare.</param>
    /// <returns>True if the left size is greater or equal to the right size.</returns>
    public static bool operator >=(KernelSize left, KernelSize right) =>
        left.CompareTo(right) >= 0;

    /// <summary>
    /// Compares the left against the right kernel size comparing by number of threads.
    /// </summary>
    /// <param name="left">The left kernel size to compare.</param>
    /// <param name="right">The right kernel size to compare.</param>
    /// <returns>True if the left size is less than the right size.</returns>
    public static bool operator <(KernelSize left, KernelSize right) =>
        left.CompareTo(right) < 0;

    /// <summary>
    /// Compares the left against the right kernel size comparing by number of threads.
    /// </summary>
    /// <param name="left">The left kernel size to compare.</param>
    /// <param name="right">The right kernel size to compare.</param>
    /// <returns>True if the left size is less or equal to the right size.</returns>
    public static bool operator <=(KernelSize left, KernelSize right) =>
        left.CompareTo(right) <= 0;
}

/// <summary>
/// Represents a kernel entry-point index.
/// </summary>
/// <param name="GridIndex">The current grid index.</param>
/// <param name="GroupIndex">The current group index.</param>
[Serializable]
[StructLayout(LayoutKind.Sequential, Size = 16)]
public readonly record struct KernelIndex(long GridIndex, int GroupIndex) :
    IComparable<KernelIndex>
{
    /// <summary>
    /// Computes the global thread index using the given group size.
    /// </summary>
    /// <param name="groupSize">The group size to use.</param>
    /// <returns>The global thread index for this thread.</returns>
    public long ComputeIndex(int groupSize) => GridIndex * groupSize + GroupIndex;

    /// <summary>
    /// Computes the global thread index using the given group size.
    /// </summary>
    /// <param name="kernelSize">The current kernel size.</param>
    /// <returns>The global thread index for this thread.</returns>
    public long ComputeIndex(in KernelSize kernelSize) =>
        ComputeIndex(kernelSize.GroupSize);

    /// <summary>
    /// Compares this kernel index to the given one by comparing grid index first.
    /// </summary>
    /// <param name="other">The other kernel index to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(KernelIndex other)
    {
        var compare = GridIndex.CompareTo(other.GridIndex);
        return Utilities.Select(
            compare != 0,
            compare,
            GroupIndex.CompareTo(other.GroupIndex));
    }

    /// <summary>
    /// Returns a string representation of this kernel index instance.
    /// </summary>
    public override string ToString() => $"({GridIndex}, {GroupIndex})";

    /// <summary>
    /// Compares the left against the right kernel index comparing by grid index first.
    /// </summary>
    /// <param name="left">The left kernel index to compare.</param>
    /// <param name="right">The right kernel index to compare.</param>
    /// <returns>True if the left index is greater than the right index.</returns>
    public static bool operator >(KernelIndex left, KernelIndex right) =>
        left.CompareTo(right) > 0;

    /// <summary>
    /// Compares the left against the right kernel index comparing by grid index first.
    /// </summary>
    /// <param name="left">The left kernel index to compare.</param>
    /// <param name="right">The right kernel index to compare.</param>
    /// <returns>True if the left index is greater or equal to the right index.</returns>
    public static bool operator >=(KernelIndex left, KernelIndex right) =>
        left.CompareTo(right) >= 0;

    /// <summary>
    /// Compares the left against the right kernel index comparing by grid index first.
    /// </summary>
    /// <param name="left">The left kernel index to compare.</param>
    /// <param name="right">The right kernel index to compare.</param>
    /// <returns>True if the left index is smaller than the right index.</returns>
    public static bool operator <(KernelIndex left, KernelIndex right) =>
        left.CompareTo(right) < 0;

    /// <summary>
    /// Compares the left against the right kernel index comparing by grid index first.
    /// </summary>
    /// <param name="left">The left kernel index to compare.</param>
    /// <param name="right">The right kernel index to compare.</param>
    /// <returns>True if the left index is smaller or equal to the right index.</returns>
    public static bool operator <=(KernelIndex left, KernelIndex right) =>
        left.CompareTo(right) <= 0;
}
