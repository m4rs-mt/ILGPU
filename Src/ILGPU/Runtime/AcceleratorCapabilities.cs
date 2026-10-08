// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime;

/// <summary>
/// Describes the hardware capabilities of an accelerator or the minimum requirements
/// of a compiled kernel. Used to verify compatibility at kernel load time.
/// </summary>
public abstract record class AcceleratorCapabilities
{
    /// <summary>
    /// Returns the accelerator type these capabilities describe.
    /// </summary>
    public abstract AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Returns true if the device (or kernel) supports the Float16 (Half) data type.
    /// </summary>
    public abstract bool Float16 { get; init; }

    /// <summary>
    /// Returns true if the device (or kernel) supports the Float64 (double) data type.
    /// </summary>
    public abstract bool Float64 { get; init; }

    /// <summary>
    /// Returns true if the device (or kernel) supports the BFloat16 data type.
    /// </summary>
    public abstract bool BFloat16 { get; init; }

    /// <summary>
    /// Returns true if the device (or kernel) supports 8-bit floating point
    /// (FP8 / E4M3 or E5M2) data types.
    /// </summary>
    public abstract bool FP8 { get; init; }

    /// <summary>
    /// Returns true if the device (or kernel) supports 4-bit floating point
    /// (FP4 / E2M1) data types.
    /// </summary>
    public abstract bool FP4 { get; init; }

    /// <summary>
    /// Returns an ordinal value representing how specific these capabilities are.
    /// Higher values indicate more specific (newer/more capable) hardware targets.
    /// Used to sort compiled kernels so best-match specializations load first.
    /// </summary>
    public abstract int SpecificityOrdinal { get; }

    /// <summary>
    /// Returns true if this device's capabilities satisfy all requirements expressed
    /// by <paramref name="kernelRequirements"/>. Non-throwing alternative to
    /// <see cref="CheckCompatibility"/>.
    /// </summary>
    /// <param name="kernelRequirements">
    /// The minimum capabilities required by a kernel.
    /// </param>
    /// <returns>True if the device can run the kernel.</returns>
    public abstract bool IsCompatible(AcceleratorCapabilities kernelRequirements);

    /// <summary>
    /// Verifies that this device's capabilities satisfy all requirements expressed by
    /// <paramref name="kernelRequirements"/>. Throws
    /// <see cref="CapabilityNotSupportedException"/> with a descriptive message naming
    /// the unsatisfied capability if incompatible.
    /// </summary>
    /// <param name="kernelRequirements">
    /// The minimum capabilities required by a kernel.
    /// </param>
    public abstract void CheckCompatibility(AcceleratorCapabilities kernelRequirements);
}
