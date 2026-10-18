// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MaxBufferSizeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Metal;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.ROCm;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Immutable;
using System.IO;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Verifies that every backend reports the maximum size of a single buffer on its
/// devices and accelerators, and that larger allocations are rejected up front.
/// </summary>
public sealed class MaxBufferSizeTests
{
    private static ImmutableArray<Device> GetDevices(BackendType backend) =>
        backend switch
        {
            BackendType.Cuda => CudaDevice.GetDevices(_ => true),
            BackendType.ROCm => ROCmDevice.GetDevices(),
            BackendType.Metal => MetalDevice.GetDevices(),
            BackendType.OpenCL => CLDevice.GetDevices(_ => true),
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };

    [Fact]
    public void CPU_HasNoBufferLimit()
    {
        using var context = Context.Create(builder => builder.CPU());
        foreach (var device in context.GetDevices<CPUDevice>())
            Assert.Equal(long.MaxValue, device.MaxBufferSize);
    }

    [SkippableTheory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void GPU_ReportsBufferLimit(BackendType backend)
    {
        Skip.IfNot(
            Availability.IsRuntimeAvailable(backend),
            $"No {backend} device available.");

        foreach (var device in GetDevices(backend))
        {
            Assert.True(device.MaxBufferSize > 0, device.ToString());

            // CUDA and HIP have no per-buffer limit beyond device memory
            if (backend is BackendType.Cuda or BackendType.ROCm)
                Assert.Equal(device.MemorySize, device.MaxBufferSize);

            // OpenCL guarantees max(global memory / 4, 128 MB) at least
            if (backend == BackendType.OpenCL)
            {
                Assert.True(
                    device.MaxBufferSize >=
                        Math.Min(
                            Math.Max(device.MemorySize / 4, 128L << 20),
                            device.MemorySize),
                    device.ToString());
                Assert.True(
                    device.MaxBufferSize <= device.MemorySize,
                    device.ToString());
            }
        }
    }

    [Fact]
    public void Accelerator_ForwardsDeviceLimit()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        Assert.Equal(
            accelerator.Device.MaxBufferSize,
            accelerator.MaxBufferSize);
    }

    [Fact]
    public void PrintInformation_ListsBufferLimit()
    {
        using var context = Context.Create(builder => builder.CPU());
        var device = context.GetPreferredDevice(preferCPU: true);
        using var writer = new StringWriter();
        device.PrintInformation(writer);
        Assert.Contains(
            "Max size of a single buffer:",
            writer.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AllocateRaw_RejectsBufferAboveLimit()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);

        // long.MaxValue elements of 2 bytes exceed the CPU limit of long.MaxValue
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accelerator.AllocateRaw(long.MaxValue, 2));
        Assert.Equal("length", exception.ParamName);
        Assert.StartsWith(
            $"Cannot allocate {long.MaxValue} elements of 2 bytes on " +
            $"'{accelerator.Name}': the buffer exceeds the maximum buffer size " +
            $"of {long.MaxValue} bytes",
            exception.Message,
            StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Metal_AllocateRaw_RejectsBufferAboveLimit()
    {
        Skip.IfNot(
            Availability.IsRuntimeAvailable(BackendType.Metal),
            "No Metal device available.");

        using var context = Context.Create(builder => builder.Metal());
        using var accelerator = context.GetMetalDevice(0)
            .CreateAccelerator(context);

        // One element more than the largest buffer fails before reaching Metal
        long length = accelerator.MaxBufferSize / sizeof(float) + 1;
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accelerator.AllocateRaw(length, sizeof(float)));
        Assert.Equal("length", exception.ParamName);

        // Buffers within the limit still allocate
        using var buffer = accelerator.AllocateRaw(1024, sizeof(float));
        Assert.Equal(1024, buffer.Length);
    }
}
