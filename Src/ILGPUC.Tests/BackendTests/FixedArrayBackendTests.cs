// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedArrayBackendTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Compiles fixed statements on local arrays with the native compilers at
/// O0 and O2. At O0, the pointer to the first element of an array whose
/// length is not a constant is merged with the null pointer in a phi, also
/// when it is cast to a void pointer, which Metal and OpenCL need in local
/// memory.
/// </summary>
public sealed class FixedArrayBackendTests
{
    public static TheoryData<BackendType, OptimizationLevel, string> KernelCases()
    {
        var data = new TheoryData<BackendType, OptimizationLevel, string>();
        var backends = new[]
        {
            BackendType.Metal,
            BackendType.Cuda,
            BackendType.ROCm,
            BackendType.OpenCL,
        };
        var kernels = new[]
        {
            nameof(LocalArrayKernels.FixedKernel),
            nameof(LocalArrayKernels.FixedNullKernel),
            nameof(LocalArrayKernels.FixedSelectKernel),
            nameof(LocalArrayKernels.FixedVoidKernel),
        };
        foreach (var backend in backends)
        {
            foreach (var level in new[] { OptimizationLevel.O0, OptimizationLevel.O2 })
            {
                foreach (var kernel in kernels)
                    data.Add(backend, level, kernel);
            }
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(KernelCases))]
    public async Task FixedArray_CompilesNatively(
        BackendType backend,
        OptimizationLevel level,
        string kernelName)
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(backend),
            $"Native compiler for {backend} is not available");

        var kernel = typeof(LocalArrayKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);
        var result = await helper.CompileToNativeAsync(
            kernel,
            backend,
            new CompilationProperties { OptimizationLevel = level });

        Assert.NotNull(result);
        Assert.True(
            result!.Success,
            $"Native compilation failed: {result.StdErr}");
    }
}
