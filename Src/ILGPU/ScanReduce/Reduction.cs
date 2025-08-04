// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Reduction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Initialization;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.ScanReduce;

/// <summary>
/// Reduce functionality for accelerators.
/// </summary>
public static class Reduction
{
    #region Lambda-Based Entry Points

    /// <summary>
    /// Performs a reduction using lambda operations.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary reduction operation.</param>
    /// <param name="atomicApply">The atomic reduction operation.</param>
    /// <returns>The reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static T Reduce<T>(
        this AcceleratorStream stream,
        ArrayView<T> input,
        T identity,
        Func<T, T, T> apply,
        AtomicApplyAction<T> atomicApply)
        where T : unmanaged =>
        stream.Reduce<T, Stride1D.Dense>(
            input.AsDense(),
            identity,
            apply,
            atomicApply);

    /// <summary>
    /// Performs a reduction using lambda operations.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary reduction operation.</param>
    /// <param name="atomicApply">The atomic reduction operation.</param>
    /// <returns>The reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel]
    public static T Reduce<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> input,
        T identity,
        Func<T, T, T> apply,
        AtomicApplyAction<T> atomicApply)
        where T : unmanaged
        where TStride : struct, IStride1D
    {
        using var output = stream.AllocateTemporary<T>(1);
        stream.Reduce(input, output.View, identity, apply, atomicApply);

        T result = default;
        output.View.CopyToCPU(stream, ref result, 1);
        return result;
    }

    /// <summary>
    /// Performs a reduction using lambda operations.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary reduction operation.</param>
    /// <param name="atomicApply">The atomic reduction operation.</param>
    [NotInsideKernel]
    public static void Reduce<T>(
        this AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        T identity,
        Func<T, T, T> apply,
        AtomicApplyAction<T> atomicApply)
        where T : unmanaged =>
        stream.Reduce<T, Stride1D.Dense>(
            input.AsDense(),
            output,
            identity,
            apply,
            atomicApply);

    /// <summary>
    /// Performs a reduction using lambda operations.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    /// <param name="identity">The identity element for the operation.</param>
    /// <param name="apply">The binary reduction operation.</param>
    /// <param name="atomicApply">The atomic reduction operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Reduce<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> input,
        ArrayView<T> output,
        T identity,
        Func<T, T, T> apply,
        AtomicApplyAction<T> atomicApply)
        where T : unmanaged
        where TStride : struct, IStride1D
    {
        if (input.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(input));
        if (output.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(output));

        // Ensure a single element in the output view
        output = output.SubView(0, 1);
        stream.Initialize(output, identity);

        // Launch reduction kernel
        var kernelConfig = stream.ComputeGridStrideKernelConfig(
            input.Length,
            out int numIterationsPerGroup);
        stream.Launch(kernelConfig, index =>
        {
            var value = identity;
            Grid.GridStrideLoop(numIterationsPerGroup, globalIndex =>
            {
                var inputValue = input[globalIndex];
                value = apply(value, inputValue);
            });
            atomicApply(ref output[0], value);
        });
    }

    #endregion
}
