// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Scanner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Runtime;
using ILGPU.Synchronization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.ScanReduce;

/// <summary>
/// Contains extension methods for scan operations.
/// </summary>
public static partial class Scanner
{
    #region Memory Management

    /// <summary>
    /// Returns true if the accelerator has the capability to perform single pass scans.
    /// </summary>
    /// <param name="accelerator">The current accelerator.</param>
    /// <returns>True if the accelerator supports single pass scans.</returns>
    [NotInsideKernel]
    public static bool SupportsSinglePassScan(this Accelerator accelerator) =>
        accelerator.AcceleratorType switch
        {
            AcceleratorType.Cuda => true,
            _ => false
        };

    /// <summary>
    /// Returns true if the stream has the capability to perform single pass scans.
    /// </summary>
    /// <param name="stream">The current accelerator stream.</param>
    /// <returns>True if the accelerator supports single pass scans.</returns>
    [NotInsideKernel]
    public static bool SupportsSinglePassScan(this AcceleratorStream stream) =>
        stream.Accelerator?.SupportsSinglePassScan() ?? false;

    /// <summary>
    /// Adds a new buffer for scan operations.
    /// </summary>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="elementSize">The size of a single element.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddScanBuffer(
        this AllocationBuilder allocationBuilder,
        int elementSize,
        long length)
    {
        if (elementSize < 1)
            throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));

        var stream = allocationBuilder.Stream;
        // Add temporary data element buffers
        if (stream.SupportsSinglePassScan())
        {
            allocationBuilder.AddBuffer(elementSize, 1);
        }
        else
        {
            allocationBuilder.AddBuffer(
                elementSize,
                stream.OptimalKernelSize.GridSize);
        }

        // Add counters
        allocationBuilder.AddBuffer<int>(2);
    }

    /// <summary>
    /// Adds a new buffer for scan operations.
    /// </summary>
    /// <typeparam name="T">The element type to sort.</typeparam>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddScanBuffer<T>(
        this AllocationBuilder allocationBuilder,
        long length) where T : unmanaged =>
        allocationBuilder.AddScanBuffer(Interop.SizeOf<T>(), length);

    #endregion

    #region Lambda-Based Entry Points

    /// <summary>
    /// Performs an inclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from.</param>
    /// <param name="target">The target view to write to.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="operation">The binary scan operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void InclusiveScan<T>(
        this AcceleratorStream stream,
        ArrayView<T> source,
        ArrayView<T> target,
        T identity,
        Func<T, T, T> operation)
        where T : unmanaged =>
        InclusiveScan<T, Stride1D.Dense, Stride1D.Dense>(
            stream,
            source.AsDense(),
            target.AsDense(),
            identity,
            operation);

    /// <summary>
    /// Performs an inclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from.</param>
    /// <param name="target">The target view to write to.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="operation">The binary scan operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void InclusiveScan<T, TSourceStride, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        T identity,
        Func<T, T, T> operation)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D =>
        GenericScan<T, TSourceStride, TTargetStride, ScanPredicates.InclusiveScan>(
            stream,
            source,
            target,
            identity,
            operation);

    /// <summary>
    /// Performs an exclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from.</param>
    /// <param name="target">The target view to write to.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="operation">The binary scan operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void ExclusiveScan<T>(
        this AcceleratorStream stream,
        ArrayView<T> source,
        ArrayView<T> target,
        T identity,
        Func<T, T, T> operation)
        where T : unmanaged =>
        ExclusiveScan<T, Stride1D.Dense, Stride1D.Dense>(
            stream,
            source.AsDense(),
            target.AsDense(),
            identity,
            operation);

    /// <summary>
    /// Performs an exclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from.</param>
    /// <param name="target">The target view to write to.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="operation">The binary scan operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void ExclusiveScan<T, TSourceStride, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        T identity,
        Func<T, T, T> operation)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D =>
        GenericScan<T, TSourceStride, TTargetStride, ScanPredicates.ExclusiveScan>(
            stream,
            source,
            target,
            identity,
            operation);

    #endregion

    #region Core Implementation

    /// <summary>
    /// Performs a generic scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from.</param>
    /// <param name="target">The target view to write to.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary scan operation.</param>
    [NotInsideKernel]
    private static void GenericScan<T, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        T identity,
        Func<T, T, T> apply)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TPredicate : struct, IScanPredicate
    {
        if (stream.AcceleratorType == AcceleratorType.Cuda)
        {
            SinglePass<T, TSourceStride, TTargetStride, TPredicate>(
                stream,
                source,
                target,
                identity,
                apply);
        }
        else
        {
            MultiPass<T, TSourceStride, TTargetStride, TPredicate>(
                stream,
                source,
                target,
                identity,
                apply);
        }
    }

    /// <summary>
    /// Performs a single pass scan operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    private static void SinglePass<T, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        T identity,
        Func<T, T, T> apply)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TPredicate : struct, IScanPredicate
    {
        if (source.Length < target.Length)
            throw new ArgumentOutOfRangeException(nameof(target));

        // Configure kernel dimensions
        var config = stream.ComputeGridStrideKernelConfig(
            source.Length,
            out int numIterationsPerGroup);

        // Get and initialize temp data
        using var tempData = stream.AllocateTemporary<T>(1);
        using var executorData = stream.AllocateTemporary<int>(1);
        executorData.View.MemSetToZero(stream);

        // Get views and launch kernel
        var tempView = tempData.View;
        var executorView = executorData.View;
        stream.Launch(config, _ =>
        {
            var executor = new SequentialGroupExecutor<T>(
                ref executorView[0],
                ref tempView[0]);

            var tileInfo = new TileInfo(source.IntLength, numIterationsPerGroup);

            // Determine our right boundary and resolve our left boundary
            T leftBoundary = identity;
            T rightBoundary = ComputeTileRightBoundary(
                tileInfo,
                source,
                identity,
                apply);

            // Sync groups and wait for the current one to become active
            leftBoundary = executor.Wait() ?? leftBoundary;

            // Wait for all threads in the group to read the same boundary value
            Group.Barrier();

            // If we are the first thread in the group, update the boundary value for
            // the next group
            var boundary = apply(leftBoundary, rightBoundary);
            executor.Release(boundary);

            // Perform the final tile scan
            ComputeTileScan<T, TSourceStride, TTargetStride, TPredicate>(
                tileInfo,
                source,
                target,
                leftBoundary,
                identity,
                apply);
        });
    }

    /// <summary>
    /// Performs a multi pass scan operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    private static void MultiPass<T, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        T identity,
        Func<T, T, T> apply)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TPredicate : struct, IScanPredicate
    {
        if (source.Length < target.Length)
            throw new ArgumentOutOfRangeException(nameof(target));

        // Configure kernel dimensions
        var config = stream.ComputeGridStrideKernelConfig(
            source.Length,
            out int numIterationsPerGroup);

        // Ensure the second pass covers all elements
        if (config.GridSize > config.GroupSize)
            throw new ArgumentOutOfRangeException(nameof(source));

        // Get and initialize temp data
        using var tempData = stream.AllocateTemporary<T>(config.GridSize);

        // Perform initial pass to determine right boundaries
        var rightBoundaries = tempData.View;
        stream.Launch(config, index =>
        {
            var tileInfo = new TileInfo(source.Length, numIterationsPerGroup);
            T rightBoundary = ComputeTileRightBoundary(
                tileInfo,
                source,
                identity,
                apply);

            if (Group.IsFirstThread)
                rightBoundaries[index.GridIndex] = rightBoundary;
        });

        // Perform second pass to adjust all offsets
        stream.Launch(config, index =>
        {
            var tileInfo = new TileInfo(source.IntLength, numIterationsPerGroup);

            var localRightBoundary = index.GroupIndex < rightBoundaries.Length
                ? rightBoundaries[index.GroupIndex]
                : identity;
            var scannedLeftBoundaries = TPredicate.ScanKind == ScanKind.Inclusive
                 ? Group.InclusiveScan(localRightBoundary, identity, apply)
                 : Group.ExclusiveScan(localRightBoundary, identity, apply);

            Trace.Assert(Grid.Index <= int.MaxValue, "Invalid grid extent");
            T leftBoundary = Group.Broadcast(scannedLeftBoundaries, (int)index.GridIndex);

            ComputeTileScan<T, TSourceStride, TTargetStride, TPredicate>(
                tileInfo,
                source,
                target,
                leftBoundary,
                identity,
                apply);
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Computes the right tile boundary.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStrideIn">The stride of the input view.</typeparam>
    /// <param name="tileInfo">The current tile info.</param>
    /// <param name="input">The input view.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary scan operation.</param>
    /// <returns>The resolved right boundary for all threads in the group.</returns>
    private static T ComputeTileRightBoundary<T, TStrideIn>(
        TileInfo tileInfo,
        ArrayView1D<T, TStrideIn> input,
        T identity,
        Func<T, T, T> apply)
        where T : unmanaged
        where TStrideIn : struct, IStride1D
    {
        T rightBoundary = tileInfo.StartIndex < tileInfo.MaxLength ?
            input[tileInfo.StartIndex] :
            identity;

        // Perform a scan of all items in this group
        rightBoundary = Group.AllReduce(rightBoundary, identity, apply);

        // Perform a linear scan over all elements in the current tile
        for (
            long i = tileInfo.StartIndex + Group.Dimension;
            i < tileInfo.EndIndex;
            i += Group.Dimension)
        {
            var inputValue = i < tileInfo.MaxLength
                ? input[i]
                : identity;

            var reduced = Group.AllReduce(inputValue, identity, apply);
            rightBoundary = apply(rightBoundary, reduced);
        }
        return rightBoundary;
    }

    /// <summary>
    /// Prepares for the next iteration of a group-wide exclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <param name="apply">The binary scan operation.</param>
    /// <returns>The starting value for the next iteration.</returns>
    private static T ExclusiveScanNextIteration<T>(
        T leftBoundary,
        T rightBoundary,
        Func<T, T, T> apply)
        where T : unmanaged
    {
        var nextBoundary = apply(leftBoundary, rightBoundary);
        var lastThreadBoundary = Group.Broadcast(new LastThreadValue<T>(nextBoundary));
        return apply(nextBoundary, lastThreadBoundary);
    }

    /// <summary>
    /// Prepares for the next iteration of a group-wide inclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <param name="apply">The binary scan operation.</param>
    /// <returns>The starting value for the next iteration.</returns>
    private static T InclusiveScanNextIteration<T>(
        T leftBoundary,
        T rightBoundary,
        Func<T, T, T> apply)
        where T : unmanaged =>
        apply(leftBoundary, rightBoundary);

    /// <summary>
    /// Computes a single scan within a single tile.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStrideIn">The stride of the input view.</typeparam>
    /// <typeparam name="TStrideOut">The stride of the output view.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="tileInfo">The current tile info.</param>
    /// <param name="input">The input view.</param>
    /// <param name="output">The output view.</param>
    /// <param name="leftBoundary">
    /// The left boundary (e.g. of the previous tile).
    /// </param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary scan operation.</param>
    private static void ComputeTileScan<
        T,
        TStrideIn,
        TStrideOut,
        TPredicate>(
        TileInfo tileInfo,
        ArrayView1D<T, TStrideIn> input,
        ArrayView1D<T, TStrideOut> output,
        T leftBoundary,
        T identity,
        Func<T, T, T> apply)
        where T : unmanaged
        where TStrideIn : struct, IStride1D
        where TStrideOut : struct, IStride1D
        where TPredicate : struct, IScanPredicate
    {
        // Fetch initial current value
        T inputValue = tileInfo.StartIndex < tileInfo.MaxLength ?
            input[tileInfo.StartIndex] :
            identity;

        // Perform a scan of all items in this group
        var current = TPredicate.ScanKind == ScanKind.Inclusive
             ? Group.InclusiveScan(inputValue, identity, apply)
             : Group.ExclusiveScan(inputValue, identity, apply);
        // Compute the right boundary (total reduction across the group)
        var rightBoundary = Group.AllReduce(inputValue, identity, apply);

        if (tileInfo.StartIndex < tileInfo.MaxLength)
            output[tileInfo.StartIndex] = apply(leftBoundary, current);

        // Adjust all scan results according to the previously computed result
        for (
            long i = tileInfo.StartIndex + Group.Dimension;
            i < tileInfo.EndIndex;
            i += Group.Dimension)
        {
            leftBoundary = TPredicate.ScanKind == ScanKind.Inclusive
                ? InclusiveScanNextIteration(leftBoundary, rightBoundary, apply)
                : ExclusiveScanNextIteration(leftBoundary, rightBoundary, apply);

            inputValue = i < tileInfo.MaxLength ? input[i] : identity;

            current = TPredicate.ScanKind == ScanKind.Inclusive
                 ? Group.InclusiveScan(inputValue, identity, apply)
                 : Group.ExclusiveScan(inputValue, identity, apply);
            rightBoundary = Group.AllReduce(inputValue, identity, apply);
            if (i < tileInfo.MaxLength)
                output[i] = apply(leftBoundary, current);
        }
    }

    #endregion
}
