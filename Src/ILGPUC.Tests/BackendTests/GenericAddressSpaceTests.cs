// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericAddressSpaceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that backends without a generic address space reject references
/// that can point to several address spaces with a clear compile-time error
/// instead of emitting them in the wrong address space.
/// </summary>
public sealed class GenericAddressSpaceTests
{
    /// <summary>
    /// A reference selected from shared and global memory, and a reference
    /// into shared memory returned by a helper method.
    /// </summary>
    [Theory]
    [InlineData(
        BackendType.Metal,
        nameof(AddressSpaceKernels.MixedSpaceRefKernel),
        "can point to global and shared memory")]
    [InlineData(
        BackendType.Metal,
        nameof(AddressSpaceKernels.SharedRefReturnKernel),
        "returned from a helper method as a generic pointer")]
    [InlineData(
        BackendType.OpenCL,
        nameof(AddressSpaceKernels.MixedSpaceRefKernel),
        "can point to global and shared memory")]
    [InlineData(
        BackendType.OpenCL,
        nameof(AddressSpaceKernels.SharedRefReturnKernel),
        "returned from a helper method as a generic pointer")]
    public void GenericReference_ReportsError(
        BackendType backend,
        string kernelName,
        string reason)
    {
        var kernel = typeof(AddressSpaceKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);

        var exception = Assert.Throws<NotSupportedException>(
            () => helper.GenerateBackendCode(kernel, backend));
        Assert.Contains($"Kernel 'AddressSpaceKernels.{kernelName}'", exception.Message);
        Assert.Contains(reason, exception.Message);
        Assert.Contains(
            $"{backend} does not support generic pointers",
            exception.Message);
    }

    /// <summary>
    /// Addresses of local variables and of shared memory stored to memory: in a
    /// view of nint and in a view of structs holding a pointer. Loaded back,
    /// they would point into global memory, so the store is reported at its
    /// location.
    /// </summary>
    [Theory]
    [InlineData(
        BackendType.Metal,
        nameof(AddressSpaceKernels.LocalAddressInViewKernel),
        "local")]
    [InlineData(
        BackendType.Metal,
        nameof(AddressSpaceKernels.SharedAddressInViewKernel),
        "shared")]
    [InlineData(
        BackendType.OpenCL,
        nameof(AddressSpaceKernels.LocalAddressInViewKernel),
        "local")]
    [InlineData(
        BackendType.OpenCL,
        nameof(AddressSpaceKernels.SharedAddressInViewKernel),
        "shared")]
    public void StoredReference_ReportsError(
        BackendType backend,
        string kernelName,
        string space)
    {
        var kernel = typeof(AddressSpaceKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);

        var exception = Assert.Throws<NotSupportedException>(
            () => helper.GenerateBackendCode(kernel, backend));
        Assert.Contains($"Kernel 'AddressSpaceKernels.{kernelName}'", exception.Message);
        Assert.Contains(
            $"points to {space} memory but is stored to memory as a generic pointer",
            exception.Message);
        Assert.Contains("AddressSpaceKernels.cs', line ", exception.Message);
        Assert.Contains(
            $"{backend} does not support generic pointers",
            exception.Message);
    }

    private const string StoredLocal =
        "points to local memory but is stored to memory as a generic pointer";

    /// <summary>
    /// Pointers to locals in structure fields and in pointer locals that cannot
    /// stay in local memory: they point to global memory on other loop
    /// iterations or, at O0, after a loop, or they are selected with pointers
    /// loaded from global memory. The error is located in the kernel instead of
    /// failing the native build.
    /// </summary>
    [Theory]
    [InlineData(
        BackendType.Metal,
        OptimizationLevel.O0,
        typeof(PointerLoopKernels),
        nameof(PointerLoopKernels.MixedFieldLoopKernel),
        "can point to global and local memory")]
    [InlineData(
        BackendType.OpenCL,
        OptimizationLevel.O1,
        typeof(PointerLoopKernels),
        nameof(PointerLoopKernels.MixedFieldLoopKernel),
        "can point to global and local memory")]
    [InlineData(
        BackendType.Metal,
        OptimizationLevel.O0,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LoadedHolderKernel),
        "points to local memory but is held in a structure field that holds " +
        "generic pointers")]
    [InlineData(
        BackendType.OpenCL,
        OptimizationLevel.O2,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LoadedHolderKernel),
        "points to local memory but is held in a structure field that holds " +
        "generic pointers")]
    [InlineData(
        BackendType.Metal,
        OptimizationLevel.O1,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LoadedPointerKernel),
        "can point to global and local memory")]
    [InlineData(
        BackendType.OpenCL,
        OptimizationLevel.O0,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LoadedPointerKernel),
        "can point to global and local memory")]
    [InlineData(
        BackendType.Metal,
        OptimizationLevel.O0,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.SwapHoldersKernel),
        "points to local memory but is held in a structure field that holds " +
        "generic pointers")]
    [InlineData(
        BackendType.OpenCL,
        OptimizationLevel.O2,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.SwapHoldersKernel),
        "points to local memory but is held in a structure field that holds " +
        "generic pointers")]
    [InlineData(
        BackendType.Metal,
        OptimizationLevel.O0,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LaterGlobalCounterKernel),
        StoredLocal)]
    [InlineData(
        BackendType.OpenCL,
        OptimizationLevel.O0,
        typeof(PointerFieldKernels),
        nameof(PointerFieldKernels.LaterGlobalCounterKernel),
        StoredLocal)]
    public void GenericLocalPointer_ReportsLocatedError(
        BackendType backend,
        OptimizationLevel opt,
        Type kernels,
        string kernelName,
        string reason)
    {
        var kernel = kernels.GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);

        var exception = Assert.Throws<NotSupportedException>(
            () => helper.GenerateBackendCode(
                kernel,
                backend,
                new CompilationProperties { OptimizationLevel = opt }));
        Assert.Contains(
            $"Kernel '{kernels.Name}.{kernelName}': a pointer or reference " +
            reason,
            exception.Message);
        Assert.Contains($"{kernels.Name}.cs', line ", exception.Message);
        Assert.Contains(
            $"{backend} does not support generic pointers",
            exception.Message);
    }

    /// <summary>
    /// A reference selected from two elements of the same address space keeps
    /// that address space.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void SingleSpaceSelect_Compiles(BackendType backend)
    {
        var kernel = typeof(AddressSpaceKernels).GetMethod(
            nameof(AddressSpaceKernels.SharedRefSelectKernel))!;
        using var helper = new CompilationHelper(backend);

        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        Assert.False(string.IsNullOrEmpty(source));
    }

    /// <summary>
    /// Pointers that provably point into a single address space compile at
    /// every optimization level, including generic pointers left in the method
    /// body when nothing is inlined and pointers to locals held in a pointer
    /// local whose address is taken.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void SingleSpacePointers_CompileAtEveryOptimizationLevel(
        BackendType backend,
        OptimizationLevel level)
    {
        var kernels = new[]
        {
            typeof(MatrixMultiplyKernels).GetMethod(
                nameof(MatrixMultiplyKernels.MatrixMultiplyTiledKernel))!,
            typeof(SharedMemoryKernels).GetMethod(
                nameof(SharedMemoryKernels.SharedMemory2DKernel))!,
            typeof(AddressSpaceKernels).GetMethod(
                nameof(AddressSpaceKernels.SharedRefSelectKernel))!,
            typeof(AddressSpaceKernels).GetMethod(
                nameof(AddressSpaceKernels.LocalAddressInLocalPointerKernel))!,
        };
        var properties = new CompilationProperties(OptimizationLevel: level);
        foreach (var kernel in kernels)
        {
            using var helper = new CompilationHelper(backend);
            var source = helper.GenerateBackendCode(kernel, backend, properties)
                .SourceCode;
            Assert.False(string.IsNullOrEmpty(source));
        }
    }

    /// <summary>
    /// Backends with a generic address space compile the same kernels.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Cuda, nameof(AddressSpaceKernels.MixedSpaceRefKernel))]
    [InlineData(BackendType.Cuda, nameof(AddressSpaceKernels.SharedRefReturnKernel))]
    [InlineData(
        BackendType.Cuda,
        nameof(AddressSpaceKernels.LocalAddressInLocalPointerKernel))]
    [InlineData(BackendType.Cuda, nameof(AddressSpaceKernels.LocalAddressInViewKernel))]
    [InlineData(BackendType.Cuda, nameof(AddressSpaceKernels.SharedAddressInViewKernel))]
    [InlineData(BackendType.ROCm, nameof(AddressSpaceKernels.MixedSpaceRefKernel))]
    [InlineData(BackendType.ROCm, nameof(AddressSpaceKernels.SharedRefReturnKernel))]
    [InlineData(
        BackendType.ROCm,
        nameof(AddressSpaceKernels.LocalAddressInLocalPointerKernel))]
    [InlineData(BackendType.ROCm, nameof(AddressSpaceKernels.LocalAddressInViewKernel))]
    [InlineData(BackendType.ROCm, nameof(AddressSpaceKernels.SharedAddressInViewKernel))]
    public void GenericReference_CompilesWithGenericAddressSpace(
        BackendType backend,
        string kernelName)
    {
        var kernel = typeof(AddressSpaceKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(backend);

        var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        Assert.False(string.IsNullOrEmpty(source));
    }
}
