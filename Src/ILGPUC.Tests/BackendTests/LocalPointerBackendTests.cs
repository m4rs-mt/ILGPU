// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalPointerBackendTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Compiles kernels whose pointers into local or shared memory are compared
/// with null or select elements of local arrays, or fields of them, with the
/// native compilers at O0 and O2. At O0, the pointers are compared before any
/// folding, also as phis of the null pointer, and the element addresses of
/// local arrays, and the field addresses derived from them, flow into phis,
/// which Metal and OpenCL need in local memory.
/// </summary>
public sealed class LocalPointerBackendTests
{
    private static readonly (Type Type, string Name)[] Kernels =
    [
        (typeof(AddressSpaceKernels), nameof(AddressSpaceKernels.LocalAddressNullKernel)),
        (typeof(AddressSpaceKernels), nameof(AddressSpaceKernels.NullPointerPhiKernel)),
        (typeof(AddressSpaceKernels), nameof(AddressSpaceKernels.NullPointerPhisKernel)),
        (typeof(AddressSpaceKernels),
            nameof(AddressSpaceKernels.SharedNullPointerKernel)),
        (typeof(LocalArrayKernels), nameof(LocalArrayKernels.RefSelectKernel)),
        (typeof(LocalArrayKernels), nameof(LocalArrayKernels.RefSelectIndexKernel)),
        (typeof(LocalArrayKernels), nameof(LocalArrayKernels.RefFieldSelectKernel)),
    ];

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
        foreach (var backend in backends)
        {
            foreach (var level in new[] { OptimizationLevel.O0, OptimizationLevel.O2 })
            {
                foreach (var (type, name) in Kernels)
                    data.Add(backend, level, $"{type.Name}.{name}");
            }
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(KernelCases))]
    public async Task LocalPointer_CompilesNatively(
        BackendType backend,
        OptimizationLevel level,
        string kernelName)
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(backend),
            $"Native compiler for {backend} is not available");

        var (type, name) = Array.Find(
            Kernels,
            kernel => $"{kernel.Type.Name}.{kernel.Name}" == kernelName);
        var kernel = type.GetMethod(name)!;
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
