// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUAcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Describes the capabilities of the CPU/Debug accelerator.
/// The CPU backend supports all common capabilities.
/// </summary>
public record class CPUAcceleratorCapabilities : AcceleratorCapabilities
{
    /// <summary>
    /// A default instance with all capabilities enabled.
    /// </summary>
    public static readonly CPUAcceleratorCapabilities Default = new()
    {
        Float16 = true,
        Float64 = true,
        BFloat16 = true,
        Int64Atomics = true,
    };

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.CPU;

    /// <inheritdoc/>
    public override bool Float16 { get; init; }

    /// <inheritdoc/>
    public override bool Float64 { get; init; }

    /// <inheritdoc/>
    public override bool BFloat16 { get; init; }

    /// <inheritdoc/>
    public override bool FP8 { get; init; }

    /// <inheritdoc/>
    public override bool FP4 { get; init; }

    /// <summary>
    /// Returns true if the device supports 64-bit atomic operations.
    /// </summary>
    public bool Int64Atomics { get; init; }

    /// <inheritdoc/>
    public override int SpecificityOrdinal => 0;

    /// <inheritdoc/>
    public override bool IsCompatible(AcceleratorCapabilities kernelRequirements) =>
        true; // CPU/Debug accelerator accepts all kernels.

    /// <inheritdoc/>
    public override void CheckCompatibility(AcceleratorCapabilities kernelRequirements)
    {
        // CPU/Debug accelerator accepts all kernels.
    }
}
