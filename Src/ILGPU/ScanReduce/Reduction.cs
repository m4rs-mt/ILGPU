// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
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
    /// <summary>
    /// Performs a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <remarks>
    /// Uses the internal cache to realize a temporary output buffer.
    /// </remarks>
    /// <returns>The reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static T Reduce<T, TReduction>(
        this AcceleratorStream stream,
        ArrayView<T> input)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        stream.Reduce<T, Stride1D.Dense, TReduction>(input.AsDense());

    /// <summary>
    /// Performs a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <remarks>
    /// Uses the internal cache to realize a temporary output buffer.
    /// </remarks>
    /// <returns>The reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel]
    public static T Reduce<T, TStride, TReduction>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> input)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TReduction : struct, IScanReduceOperation<T>
    {
        // Get temp buffer
        using var output = stream.AllocateTemporary<T>(1);
        stream.Reduce<T, TStride, TReduction>(input, output.View);

        // Copy back to CPU memory
        T result = default;
        output.View.CopyToCPU(stream, ref result, 1);
        return result;
    }

    /// <summary>
    /// Performs a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    [NotInsideKernel]
    public static void Reduce<T, TReduction>(
        this AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        stream.Reduce<T, Stride1D.Dense, TReduction>(input.AsDense(), output);

    /// <summary>
    /// Performs a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Reduce<T, TStride, TReduction>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> input,
        ArrayView<T> output)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TReduction : struct, IScanReduceOperation<T>
    {
        if (input.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(input));
        if (output.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(output));

        // Ensure a single element in the output view
        output = output.SubView(0, 1);
        stream.Initialize(output, TReduction.Identity);

        // Launch reduction kernel
        var kernelConfig = stream.ComputeGridStrideKernelConfig(
            input.Length,
            out int numIterationsPerGroup);
        stream.Launch(kernelConfig, index =>
        {
            var value = TReduction.Identity;
            Grid.GridStrideLoop(numIterationsPerGroup, globalIndex =>
            {
                var inputValue = input[globalIndex];
                value = TReduction.Apply(value, inputValue);
            });
            TReduction.AtomicApply(ref output[0], value);
        });
    }
}
