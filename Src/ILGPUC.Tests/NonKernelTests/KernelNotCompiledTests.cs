// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelNotCompiledTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Initialization;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Launches kernels whose launch sites ILGPUC did not compile (this test assembly
/// is not built with ILGPUC) on a CPU accelerator and verifies that they report
/// the kernel, the accelerator type and the reason.
/// </summary>
public sealed class KernelNotCompiledTests : IDisposable
{
    private const string TestClassName =
        "ILGPUC.Tests.NonKernelTests.KernelNotCompiledTests";

    private readonly Context _context;
    private readonly Accelerator _accelerator;

    public KernelNotCompiledTests()
    {
        _context = Context.Create(builder => builder.CPU());
        _accelerator = _context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(_context);
    }

    private AcceleratorStream Stream => _accelerator.DefaultStream;

    private static void IndexKernel(Index1D index) =>
        GC.KeepAlive(index);

    /// <summary>
    /// Asserts that the given launch reports the kernel as not compiled.
    /// </summary>
    private static void AssertNotCompiled(Action launch, string kernelName)
    {
        var exception = Assert.Throws<KernelNotCompiledException>(launch);
        Assert.Equal(kernelName, exception.KernelName);
        Assert.Equal(AcceleratorType.CPU, exception.AcceleratorType);
        Assert.StartsWith(
            $"Kernel '{kernelName}' was not compiled for CPU: " +
            "ILGPUC did not compile its launch site.",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("Initialize", exception.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<NotSupportedException>(exception);
    }

    [Fact]
    public void Launch1D_ReportsLambdaKernel()
    {
        using var buffer = Stream.Allocate1D<int>(4);
        var view = buffer.View;
        AssertNotCompiled(
            () => Stream.Launch((Index1D)4, index => view[index] = index),
            $"{TestClassName}.{nameof(Launch1D_ReportsLambdaKernel)}");
    }

    [Fact]
    public void LaunchLong1D_ReportsLambdaKernel()
    {
        using var buffer = Stream.Allocate1D<long>(4);
        var view = buffer.View;
        AssertNotCompiled(
            () => Stream.Launch((LongIndex1D)4L, index => view[index] = index),
            $"{TestClassName}.{nameof(LaunchLong1D_ReportsLambdaKernel)}");
    }

    [Fact]
    public void Launch1D_ReportsMethodGroupKernel() =>
        AssertNotCompiled(
            () => Stream.Launch((Index1D)4, IndexKernel),
            $"{TestClassName}.{nameof(IndexKernel)}");

    [Fact]
    public void LaunchGrouped_ReportsLambdaKernel()
    {
        using var buffer = Stream.Allocate1D<int>(4);
        var view = buffer.View;
        AssertNotCompiled(
            () => Stream.Launch(
                new KernelConfig(1, 4),
                index => view[index.GroupIndex] = index.GroupIndex),
            $"{TestClassName}.{nameof(LaunchGrouped_ReportsLambdaKernel)}");
    }

    [Fact]
    public void Launch2D_ReportsLambdaKernel()
    {
        using var buffer = Stream.Allocate1D<int>(4);
        var view = buffer.View;
        AssertNotCompiled(
            () => Stream.Launch(new Index2D(2, 2), index => view[index.X] = index.Y),
            $"{TestClassName}.{nameof(Launch2D_ReportsLambdaKernel)}");
        AssertNotCompiled(
            () => Stream.Launch2D<Stride2D.DenseX>(
                new Index2D(2, 2),
                index => view[index.X] = index.Y),
            $"{TestClassName}.{nameof(Launch2D_ReportsLambdaKernel)}");
    }

    [Fact]
    public void Launch3D_ReportsLambdaKernel()
    {
        using var buffer = Stream.Allocate1D<int>(4);
        var view = buffer.View;
        AssertNotCompiled(
            () => Stream.Launch(new Index3D(2, 2, 2), index => view[index.X] = index.Z),
            $"{TestClassName}.{nameof(Launch3D_ReportsLambdaKernel)}");
        AssertNotCompiled(
            () => Stream.Launch3D(
                new LongIndex3D(2, 2, 2),
                index => view[index.X] = (int)index.Z),
            $"{TestClassName}.{nameof(Launch3D_ReportsLambdaKernel)}");
    }

    [Fact]
    public void Initialize_ReportsLibraryKernel()
    {
        using var buffer = Stream.Allocate1D<int>(4);
        AssertNotCompiled(
            () => Stream.Initialize(buffer.View, 23),
            "ILGPU.Initialization.Initializer.Initialize");
    }

    [Fact]
    public void Transform_ReportsLibraryKernel()
    {
        using var source = Stream.Allocate1D<int>(4);
        using var target = Stream.Allocate1D<int>(4);
        AssertNotCompiled(
            () => Stream.Transform(source.View, target.View, value => value + 1),
            "ILGPU.Initialization.Transformer.Transform");
    }

    public void Dispose()
    {
        _accelerator.Dispose();
        _context.Dispose();
    }
}
