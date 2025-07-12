// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformer.cs
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
public static class Transformer
{
    /// <summary>
    /// Transforms input sequences into transformed output sequences.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The target element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source view to read from and to transform.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="transformer">The transformer function to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Transform<TSource, TTarget>(
        this AcceleratorStream stream,
        ArrayView<TSource> source,
        ArrayView<TTarget> target,
        Func<TSource, TTarget> transformer)
        where TSource : unmanaged
        where TTarget : unmanaged =>
        stream.Launch(target.Extent, index =>
            target[index] = transformer(source[index]));

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
    /// <param name="transformer">The transformer function to use.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Transform<TSource, TSourceStride, TTarget, TTargetStride>(
        this AcceleratorStream stream,
        ArrayView1D<TSource, TSourceStride> source,
        ArrayView1D<TTarget, TTargetStride> target,
        Func<TSource, TTarget> transformer)
        where TSource : unmanaged
        where TTarget : unmanaged
        where TSourceStride : struct, IStride1D
        where TTargetStride : struct, IStride1D =>
        stream.Launch(target.Extent, index =>
            target[index] = transformer(source[index]));
}
