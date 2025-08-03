// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Represents a CPU device descriptor for vectorized CPU execution.
/// </summary>
public sealed class CPUDevice : Device, IDeviceAcceleratorTypeInfo
{
    /// <summary>
    /// Default device: array-based vectorization with 8 lanes.
    /// Hardware acceleration depends on <see cref="Vector.IsHardwareAccelerated"/>.
    /// </summary>
    public static readonly CPUDevice Default = new(
        vectorWidth: Vector<int>.Count,
        isHardwareAccelerated: Vector.IsHardwareAccelerated);

    /// <summary>
    /// Creates a CPU device that emulates a specific warp size.
    /// Useful for testing kernels targeting GPUs with specific warp sizes
    /// (e.g., 32 for CUDA, 64 for AMD) on the CPU.
    /// </summary>
    public static CPUDevice Emulated(int warpSize) => new(
        vectorWidth: warpSize,
        isHardwareAccelerated: false);

    /// <summary>
    /// Creates a new CPU device descriptor.
    /// </summary>
    /// <param name="vectorWidth">The SIMD vector width (number of lanes).</param>
    /// <param name="isHardwareAccelerated">
    /// When <see langword="true"/>, the vector width maps to actual SIMD hardware
    /// instructions. When <see langword="false"/>, vectorization uses scalar arrays.
    /// </param>
    internal CPUDevice(
        int vectorWidth,
        bool isHardwareAccelerated = false) : base(AcceleratorType.CPU)
    {
        if (vectorWidth < 1)
            throw new ArgumentOutOfRangeException(nameof(vectorWidth));

        VectorWidth = vectorWidth;
        IsHardwareAccelerated = isHardwareAccelerated;
        Name = isHardwareAccelerated
            ? "CPU (SIMD)"
            : $"CPU (Emulated w{vectorWidth})";
        WarpSize = vectorWidth;
        MaxNumThreadsPerGroup = vectorWidth;
        MaxNumThreadsPerMultiprocessor = vectorWidth;
        NumMultiprocessors = Environment.ProcessorCount;
        MaxSharedMemoryPerGroup = int.MaxValue;
        MaxConstantMemory = int.MaxValue;
        MemorySize = long.MaxValue;
        // GroupSize = vectorWidth so that ComputeKernelConfig(n) returns
        // (ceil(n/vectorWidth), vectorWidth). The CPU launcher bounds its
        // loop on userExtent (the original n), not gridSize * groupSize,
        // so extra lanes are safely masked out.
        OptimalKernelSize = new KernelSize(
            NumMultiprocessors, MaxNumThreadsPerGroup);
        Capabilities = CPUAcceleratorCapabilities.Default;
    }

    /// <summary>
    /// Returns the SIMD vector width (number of lanes).
    /// </summary>
    public int VectorWidth { get; }

    /// <summary>
    /// <see langword="true"/> when the vector width maps to actual SIMD hardware
    /// instructions (<see cref="Vector{T}"/>, Vector256, etc.).
    /// <see langword="false"/> for pure software emulation using scalar arrays.
    /// </summary>
    public bool IsHardwareAccelerated { get; }

    /// <inheritdoc/>
    static AcceleratorType IDeviceAcceleratorTypeInfo.AcceleratorType =>
        AcceleratorType.CPU;

    /// <inheritdoc/>
    public override Accelerator CreateAccelerator(Context context) =>
        new CPUAccelerator(context, this);
}
