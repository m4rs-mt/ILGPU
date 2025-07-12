// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Initializer.cs
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
/// Represents initialization helpers for views.
/// </summary>
public static partial class Initializer
{
    /// <summary>
    /// Initializes all values in the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="value">The value to write into the view.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Initialize<T>(
        this AcceleratorStream stream,
        ArrayView<T> target,
        T value)
        where T : unmanaged =>
        stream.Launch(target.Extent, index => target[index] = value);

    /// <summary>
    /// Initializes all values in the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="value">The value to write into the view.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Initialize<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> target,
        T value)
        where T : unmanaged
        where TStride : struct, IStride1D =>
        stream.Launch(target.Extent, index => target[index] = value);

    /// <summary>
    /// Initializes all values in the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="initializer">
    /// A function to get an initialization value for each element.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Initialize<T>(
        this AcceleratorStream stream,
        ArrayView<T> target,
        Func<long, T> initializer)
        where T : unmanaged =>
        stream.Launch(target.Extent, index => target[index] = initializer(index));

    /// <summary>
    /// Initializes all values in the given view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="initializer">
    /// A function to get an initialization value for each element.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void Initialize<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> target,
        Func<long, T> initializer)
        where T : unmanaged
        where TStride : struct, IStride1D =>
        stream.Launch(target.Extent, index => target[index] = initializer(index));
}
