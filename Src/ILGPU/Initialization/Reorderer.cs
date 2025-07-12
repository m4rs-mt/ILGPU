// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Reorderer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Initialization;

/// <summary>
/// Represents transformation helpers for views.
/// </summary>
public static class Reorderer
{
    /// <summary>
    /// Transforms input sequences into transformed output sequences.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="mapping">The index mapping transformer to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Reorder<T>(
        this AcceleratorStream stream,
        ArrayView<T> source,
        ArrayView<T> target,
        Func<LongIndex1D, T, LongIndex1D> mapping)
        where T : unmanaged =>
        stream.Launch(target.Extent, index =>
        {
            var sourceValue = source[index];
            var targetIndex = mapping(index, sourceValue);
            target[targetIndex] = sourceValue;
        });

    /// <summary>
    /// Transforms input sequences into transformed output sequences.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="mapping">The index mapping transformer to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Reorder<T, TSourceStride, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TSourceStride> source,
        ArrayView1D<T, TTargetStride> target,
        Func<LongIndex1D, T, LongIndex1D> mapping)
        where T : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D =>
        stream.Launch(target.Extent, index =>
        {
            var sourceValue = source[index];
            var targetIndex = mapping(index, sourceValue);
            target[targetIndex] = sourceValue;
        });

    /// <summary>
    /// Transforms input sequences into transformed output sequences.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The target element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="mapping">The index mapping transformer to use.</param>
    /// <param name="transformer">The transformer function to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Reorder<TSource, TTarget>(
        this AcceleratorStream stream,
        ArrayView<TSource> source,
        ArrayView<TTarget> target,
        Func<LongIndex1D, TSource, LongIndex1D> mapping,
        Func<TSource, TTarget> transformer)
        where TSource : unmanaged
        where TTarget : unmanaged =>
        stream.Launch(target.Extent, index =>
        {
            var sourceValue = source[index];
            var targetIndex = mapping(index, sourceValue);
            target[targetIndex] = transformer(sourceValue);
        });

    /// <summary>
    /// Transforms input sequences into transformed output sequences.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TSourceStride">The source view stride.</typeparam>
    /// <typeparam name="TTarget">The target element type.</typeparam>
    /// <typeparam name="TTargetStride">The target view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="mapping">The index mapping transformer to use.</param>
    /// <param name="transformer">The transformer function to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Reorder<TSource, TSourceStride, TTarget, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<TSource, TSourceStride> source,
        ArrayView1D<TTarget, TTargetStride> target,
        Func<LongIndex1D, TSource, LongIndex1D> mapping,
        Func<TSource, TTarget> transformer)
        where TSource : unmanaged
        where TTarget : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D =>
        stream.Launch(target.Extent, index =>
        {
            var sourceValue = source[index];
            var targetIndex = mapping(index, sourceValue);
            target[targetIndex] = transformer(sourceValue);
        });
}
