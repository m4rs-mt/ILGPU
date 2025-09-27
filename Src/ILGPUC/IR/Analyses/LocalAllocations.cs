// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalAllocations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System.Collections.Immutable;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents information about an alloca node.
/// </summary>
/// <param name="Index">The allocation index.</param>
/// <param name="Alloca">The alloca node.</param>
readonly record struct AllocaInformation(int Index, Alloca Alloca)
{
    /// <summary>
    /// Returns the number of array elements.
    /// </summary>
    public int ArraySize { get; } = ComputeArraySize(Alloca);

    /// <summary>
    /// Returns true if this is an array.
    /// </summary>
    public bool IsArray => ArraySize > 1;

    /// <summary>
    /// Returns true if this is an array with dynamic length.
    /// </summary>
    public bool IsDynamicArray => ArraySize < 0;

    /// <summary>
    /// Returns the element size in bytes of a single element.
    /// </summary>
    public int ElementSize => Alloca.AllocType.Size;

    /// <summary>
    /// Returns the element alignment in bytes of a single element.
    /// </summary>
    public int ElementAlignment => Alloca.AllocType.Alignment;

    /// <summary>
    /// Returns the total size in bytes.
    /// </summary>
    public int TotalSize => IsDynamicArray ? 0 : ElementSize * ArraySize;

    /// <summary>
    /// Returns the element type.
    /// </summary>
    public TypeValue ElementType => Alloca.AllocType;

    /// <summary>
    /// Computes the array size for an alloca node.
    /// </summary>
    private static int ComputeArraySize(Alloca alloca)
    {
        if (alloca.IsStaticAllocation(out var length))
            return length.Int32Value;
        if (alloca.IsSimpleAllocation())
            return 1;
        if (alloca.IsDynamicAllocation())
            return -1; // Size determined at run-time

        throw alloca.Location.GetNotSupportedException(
            ErrorMessages.NotSupportedDynamicAllocation,
            alloca.AllocType);
    }
}

/// <summary>
/// Implements an alloca analysis to resolve information about local alloca nodes.
/// </summary>
/// <param name="Allocations">All local allocations.</param>
/// <param name="TotalSize">The total size of all allocations in bytes.</param>
readonly record struct LocalAllocations(
    ImmutableArray<AllocaInformation> Allocations,
    int TotalSize)
{
    /// <summary>
    /// Creates an alloca analysis.
    /// </summary>
    /// <typeparam name="TOrder">The traversal order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="collection">The block collection.</param>
    public static LocalAllocations Create<TOrder, TDirection>(
        in BasicBlockCollection<TOrder, TDirection> collection)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TDirection : struct, IControlFlowDirection
    {
        var allocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
        var dynamicAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
        int totalSize = 0;

        collection.ForEachValue<Alloca>(alloca =>
        {
            var info = new AllocaInformation(allocations.Count, alloca);
            if (info.IsDynamicArray)
            {
                dynamicAllocations.Add(info);
            }
            else
            {
                allocations.Add(info);
                totalSize += info.TotalSize;
            }
        });

        // Add dynamic allocations at the end
        allocations.AddRange(dynamicAllocations);

        return new LocalAllocations(allocations.ToImmutable(), totalSize);
    }

    /// <summary>
    /// Returns the i-th allocation.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The resolved alloca information.</returns>
    public AllocaInformation this[int index] => Allocations[index];

    /// <summary>
    /// Returns the number of allocations.
    /// </summary>
    public int Length => Allocations.Length;

    /// <summary>
    /// Returns an enumerator to enumerate all allocas.
    /// </summary>
    /// <returns>An enumerator to enumerate all allocas.</returns>
    public ImmutableArray<AllocaInformation>.Enumerator GetEnumerator() =>
        Allocations.GetEnumerator();
}

/// <summary>
/// Stores alignment information for allocations.
/// </summary>
/// <param name="alignmentInfo">The underlying pointer alignment information.</param>
readonly struct LocalAllocationAlignments(AlignmentInfo alignmentInfo)
{
    /// <summary>
    /// Empty allocation alignment information.
    /// </summary>
    public static readonly LocalAllocationAlignments Empty = new(AlignmentInfo.Empty);

    /// <summary>
    /// Creates allocation alignment information for the given method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <param name="globalAlignment">
    /// The initial alignment information of all pointers and views.
    /// </param>
    public static LocalAllocationAlignments Create(Method method, int globalAlignment)
    {
        var alignmentInfo = PointerAlignments.Apply(method, globalAlignment);
        return new LocalAllocationAlignments(alignmentInfo);
    }

    /// <summary>
    /// Returns the alignment in bytes for the given alloca.
    /// </summary>
    /// <param name="alloca">The alloca to get alignment for.</param>
    /// <returns>The alignment in bytes.</returns>
    public int this[Alloca alloca] => alignmentInfo[alloca];

    /// <summary>
    /// Returns true if this alignment information object is empty.
    /// </summary>
    public bool IsEmpty => alignmentInfo.IsEmpty;

    /// <summary>
    /// Gets the alignment for an alloca with a safe minimum.
    /// </summary>
    /// <param name="alloca">The alloca to get alignment for.</param>
    /// <param name="safeMinAlignment">The safe minimum alignment in bytes.</param>
    /// <returns>The computed alignment.</returns>
    public int GetAlignment(Alloca alloca, int safeMinAlignment) =>
        alignmentInfo.GetAlignment(alloca, safeMinAlignment);

    /// <summary>
    /// Gets the alignment for an alloca with a safe minimum based on type.
    /// </summary>
    /// <param name="alloca">The alloca to get alignment for.</param>
    /// <param name="safeMinTypeAlignment">The safe minimum type alignment.</param>
    /// <returns>The computed alignment.</returns>
    public int GetAlignment(Alloca alloca, TypeValue safeMinTypeAlignment) =>
        alignmentInfo.GetAlignment(alloca, safeMinTypeAlignment);
}
