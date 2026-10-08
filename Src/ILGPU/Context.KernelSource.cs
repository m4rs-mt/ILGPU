// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.KernelSource.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ILGPU.Runtime;

namespace ILGPU;

/// <summary>
/// Represents the main ILGPU context.
/// </summary>
/// <remarks>Members of this class are thread-safe.</remarks>
partial class Context
{
    private static readonly List<CompiledKernel>?[] _compiledKernels =
        new List<CompiledKernel>?[(int)AcceleratorType.NumAcceleratorTypes];

    /// <summary>
    /// Returns the number of registered compiled kernels.
    /// </summary>
    public static int NumCompiledKernels { get; private set; }

    /// <summary>
    /// Returns the number of compiled kernels.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>The number of compiled kernels for the given accelerator type.</returns>
    public static int GetNumCompiledKernels(AcceleratorType acceleratorType) =>
        _compiledKernels[(int)acceleratorType]?.Count ?? 0;

    /// <summary>
    /// Registers the given kernels with the context.
    /// </summary>
    /// <typeparam name="TKernel">The kernel type.</typeparam>
    /// <param name="kernelsToAdd">The kernels to register.</param>
    public static void RegisterKernels<TKernel>(IReadOnlyList<TKernel> kernelsToAdd)
        where TKernel : CompiledKernel, ICompiledKernelKind
    {
        ref var kernels = ref _compiledKernels[(int)TKernel.GeneralAcceleratorType];
        kernels ??= new(kernelsToAdd.Count);

        kernels.AddRange(kernelsToAdd);
        NumCompiledKernels += kernels.Count;
    }

    /// <summary>
    /// Executes the given callback for each compiled kernel known.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="callback">The callback to invoke for each kernel.</param>
    internal static void ForEachCompiledKernel(
        AcceleratorType acceleratorType,
        Action<CompiledKernel> callback)
    {
        var kernels = _compiledKernels[(int)acceleratorType];
        if (kernels is null) return;

        foreach (var kernel in kernels)
            callback(kernel);
    }
}
