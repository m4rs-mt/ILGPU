// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.KernelSource.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Collections.Generic;

namespace ILGPU;

/// <summary>
/// Represents the main ILGPU context.
/// </summary>
/// <remarks>Members of this class are thread-safe.</remarks>
partial class Context
{
    private static readonly List<CompiledKernel> _compiledKernels = [];

    /// <summary>
    /// Loads compiled kernels from the given source.
    /// </summary>
    /// <param name="source">The source to load kernels from.</param>
    public static void LoadCompiledKernels(CompiledKernelSource source)
    {
        foreach (var compiledKernel in source)
            _compiledKernels.Add(compiledKernel);
    }

    /// <summary>
    /// Returns the number of registered compiled kernels.
    /// </summary>
    internal static int NumCompiledKernels => _compiledKernels.Count;

    /// <summary>
    /// Executes the given callback for each compiled kernel known.
    /// </summary>
    /// <param name="callback">The callback to invoke for each kernel.</param>
    internal static void ForEachCompiledKernel(Action<CompiledKernel, int> callback)
    {
        for (int i = 0; i < _compiledKernels.Count; ++i)
            callback(_compiledKernels[i], i);
    }
}
