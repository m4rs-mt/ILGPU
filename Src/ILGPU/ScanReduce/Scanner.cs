// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
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

    #region Entry Points

    /// <summary>
    /// Performs an inclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void InclusiveScan<T, TScan>(
        this AcceleratorStream stream,
        ArrayView<T> source,
        ArrayView<T> target)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        InclusiveScan<T, TScan, Stride1D.Dense, Stride1D.Dense>(
            stream,
            source.AsDense(),
            target.AsDense());

    /// <summary>
    /// Performs an inclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void InclusiveScan<T, TScan, TSourceStride, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T> =>
        GenericScan<T, TScan, TSourceStride, TTargetStride, ScanPredicates.InclusiveScan>(
            stream,
            source,
            target);

    /// <summary>
    /// Performs an exclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void ExclusiveScan<T, TScan>(
        this AcceleratorStream stream,
        ArrayView<T> source,
        ArrayView<T> target)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        ExclusiveScan<T, TScan, Stride1D.Dense, Stride1D.Dense>(
            stream,
            source.AsDense(),
            target.AsDense());

    /// <summary>
    /// Performs an exclusive scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void ExclusiveScan<T, TScan, TSourceStride, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T> =>
        GenericScan<T, TScan, TSourceStride, TTargetStride, ScanPredicates.ExclusiveScan>(
            stream,
            source,
            target);

    /// <summary>
    /// Performs a generic scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [NotInsideKernel]
    public static void GenericScan<T, TScan, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T>
        where TPredicate : struct, IScanPredicate
    {
        if (stream.AcceleratorType == AcceleratorType.Cuda)
        {
            SinglePass<T, TScan, TSourceStride, TTargetStride, TPredicate>(
                stream,
                source,
                target);
        }
        else
        {
            MultiPass<T, TScan, TSourceStride, TTargetStride, TPredicate>(
                stream,
                source,
                target);
        }
    }

    /// <summary>
    /// Performs a single pass scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    private static void SinglePass<T, TScan, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T>
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
            T leftBoundary = TScan.Identity;
            T rightBoundary = ComputeTileRightBoundary<T, TSourceStride, TScan>(
                tileInfo,
                source);

            // Sync groups and wait for the current one to become active
            leftBoundary = executor.Wait() ?? leftBoundary;

            // Wait for all threads in the group to read the same boundary value
            Group.Barrier();

            // If we are the first thread in the group, update the boundary value for
            // the next group
            var boundary = TScan.Apply(leftBoundary, rightBoundary);
            executor.Release(boundary);

            // Perform the final tile scan
            ComputeTileScan<T, TSourceStride, TTargetStride, TScan, TPredicate>(
                tileInfo,
                source,
                target,
                leftBoundary);
        });
    }

    /// <summary>
    /// Performs a multi pass scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The operation implementation.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    private static void MultiPass<T, TScan, TSourceStride, TTargetStride, TPredicate>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T>
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
            T rightBoundary = ComputeTileRightBoundary<T, TSourceStride, TScan>(
                tileInfo,
                source);

            if (Group.IsFirstThread)
                rightBoundaries[index.GridIndex] = rightBoundary;
        });

        // Perform second pass to adjust all offsets
        stream.Launch(config, index =>
        {
            var tileInfo = new TileInfo(source.IntLength, numIterationsPerGroup);

            var localRightBoundary = index.GroupIndex < rightBoundaries.Length
                ? rightBoundaries[index.GroupIndex]
                : TScan.Identity;
            var scannedLeftBoundaries = TPredicate.ScanKind == ScanKind.Inclusive
                 ? Group.InclusiveScan<T, TScan>(localRightBoundary)
                 : Group.ExclusiveScan<T, TScan>(localRightBoundary);

            Trace.Assert(Grid.Index <= int.MaxValue, "Invalid grid extent");
            T leftBoundary = Group.Broadcast(scannedLeftBoundaries, (int)index.GridIndex);

            ComputeTileScan<T, TSourceStride, TTargetStride, TScan, TPredicate>(
                tileInfo,
                source,
                target,
                leftBoundary);
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Computes the right tile boundary.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStrideIn">The stride of the input view.</typeparam>
    /// <typeparam name="TScan">The scan-operation type.</typeparam>
    /// <param name="tileInfo">The current tile info.</param>
    /// <param name="input">The input view.</param>
    /// <returns>The resolved right boundary for all threads in the group.</returns>
    private static T ComputeTileRightBoundary<T, TStrideIn, TScan>(
        TileInfo tileInfo,
        ArrayView1D<T, TStrideIn> input)
        where T : unmanaged
        where TStrideIn : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T>
    {
        T rightBoundary = tileInfo.StartIndex < tileInfo.MaxLength ?
            input[tileInfo.StartIndex] :
            TScan.Identity;

        // Perform a scan of all items in this group
        rightBoundary = Group.AllReduce<T, TScan>(rightBoundary);

        // Perform a linear scan over all elements in the current tile
        for (
            long i = tileInfo.StartIndex + Group.Dimension;
            i < tileInfo.EndIndex;
            i += Group.Dimension)
        {
            var inputValue = i < tileInfo.MaxLength
                ? input[i]
                : TScan.Identity;

            var reduced = Group.AllReduce<T, TScan>(inputValue);
            rightBoundary = TScan.Apply(rightBoundary, reduced);
        }
        return rightBoundary;
    }

    /// <summary>
    /// Prepares for the next iteration of a group-wide exclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <returns>The starting value for the next iteration.</returns>
    private static T ExclusiveScanNextIteration<T, TScan>(T leftBoundary, T rightBoundary)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T>
    {
        var nextBoundary = TScan.Apply(leftBoundary, rightBoundary);
        var lastThreadBoundary = Group.Broadcast(new LastThreadValue<T>(nextBoundary));
        return TScan.Apply(nextBoundary, lastThreadBoundary);
    }

    /// <summary>
    /// Prepares for the next iteration of a group-wide inclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <returns>The starting value for the next iteration.</returns>
    public static T InclusiveScanNextIteration<T, TScan>(T leftBoundary, T rightBoundary)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        TScan.Apply(leftBoundary, rightBoundary);

    /// <summary>
    /// Computes a single scan within a single tile.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStrideIn">The stride of the input view.</typeparam>
    /// <typeparam name="TStrideOut">The stride of the output view.</typeparam>
    /// <typeparam name="TScan">The scan-operation type.</typeparam>
    /// <typeparam name="TPredicate">The scan predicate type.</typeparam>
    /// <param name="tileInfo">The current tile info.</param>
    /// <param name="input">The input view.</param>
    /// <param name="output">The output view.</param>
    /// <param name="leftBoundary">
    /// The left boundary (e.g. of the previous tile).
    /// </param>
    private static void ComputeTileScan<
        T,
        TStrideIn,
        TStrideOut,
        TScan,
        TPredicate>(
        TileInfo tileInfo,
        ArrayView1D<T, TStrideIn> input,
        ArrayView1D<T, TStrideOut> output,
        T leftBoundary)
        where T : unmanaged
        where TStrideIn : struct, IStride1D
        where TStrideOut : struct, IStride1D
        where TScan : struct, IScanReduceOperation<T>
        where TPredicate : struct, IScanPredicate
    {
        // Fetch initial current value
        T inputValue = tileInfo.StartIndex < tileInfo.MaxLength ?
            input[tileInfo.StartIndex] :
            TScan.Identity;

        // Perform a scan of all items in this group
        var current = TPredicate.ScanKind == ScanKind.Inclusive
             ? Group.InclusiveScan<T, TScan>(inputValue, out var localBoundaries)
             : Group.ExclusiveScan<T, TScan>(inputValue, out localBoundaries);

        if (tileInfo.StartIndex < tileInfo.MaxLength)
            output[tileInfo.StartIndex] = TScan.Apply(leftBoundary, current);

        // Adjust all scan results according to the previously computed result
        for (
            long i = tileInfo.StartIndex + Group.Dimension;
            i < tileInfo.EndIndex;
            i += Group.Dimension)
        {
            leftBoundary = TPredicate.ScanKind == ScanKind.Inclusive
                ? InclusiveScanNextIteration<T, TScan>(
                    leftBoundary,
                    localBoundaries.RightBoundary)
                : ExclusiveScanNextIteration<T, TScan>(
                    leftBoundary,
                    localBoundaries.RightBoundary);

            inputValue = i < tileInfo.MaxLength ? input[i] : TScan.Identity;

            current = TPredicate.ScanKind == ScanKind.Inclusive
                 ? Group.InclusiveScan<T, TScan>(inputValue, out localBoundaries)
                 : Group.ExclusiveScan<T, TScan>(inputValue, out localBoundaries);
            if (i < tileInfo.MaxLength)
                output[i] = TScan.Apply(leftBoundary, current);
        }
    }

    #endregion
}
