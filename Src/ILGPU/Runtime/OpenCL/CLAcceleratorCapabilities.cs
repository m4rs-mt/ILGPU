// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLAcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System.Collections.Immutable;

namespace ILGPU.Runtime.OpenCL;

/// <summary>
/// Describes the capabilities of an OpenCL accelerator.
/// </summary>
public record class CLAcceleratorCapabilities : AcceleratorCapabilities
{
    #region Static

    /// <summary>
    /// Extensions required for Float16 support.
    /// </summary>
    internal static readonly ImmutableArray<string> Float16Extensions =
        ImmutableArray.Create("cl_khr_fp16");

    /// <summary>
    /// Extensions required for Float64 support.
    /// </summary>
    internal static readonly ImmutableArray<string> Float64Extensions =
        ImmutableArray.Create("cl_khr_fp64");

    /// <summary>
    /// Extensions required for 64-bit atomics support.
    /// </summary>
    internal static readonly ImmutableArray<string> Int64AtomicsExtensions =
        ImmutableArray.Create(
            "cl_khr_int64_base_atomics",
            "cl_khr_int64_extended_atomics");

    private static void CheckFlag(string name, bool device, bool required)
    {
        if (required && !device)
            throw new CapabilityNotSupportedException(
                $"Kernel requires OpenCL capability '{name}' which is not available on " +
                $"this device.");
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.OpenCL;

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
    /// Returns true if generic address space is supported.
    /// </summary>
    public bool GenericAddressSpace { get; init; }

    /// <summary>
    /// Returns true if 64-bit atomic operations are supported.
    /// </summary>
    public bool Int64Atomics { get; init; }

    /// <summary>
    /// Returns true if sub-groups are supported.
    /// </summary>
    public bool SubGroups { get; init; }

    /// <summary>
    /// Returns the list of OpenCL extensions implied by the active capability flags.
    /// </summary>
    public ImmutableArray<string> Extensions
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            if (Float16)      builder.AddRange(Float16Extensions);
            if (Float64)      builder.AddRange(Float64Extensions);
            if (Int64Atomics) builder.AddRange(Int64AtomicsExtensions);
            return builder.ToImmutable();
        }
    }

    #endregion

    #region AcceleratorCapabilities

    /// <inheritdoc/>
    public override int SpecificityOrdinal => 0;

    /// <inheritdoc/>
    public override bool IsCompatible(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not CLAcceleratorCapabilities req)
            return false;
        if (req.Float16 && !Float16) return false;
        if (req.Float64 && !Float64) return false;
        if (req.GenericAddressSpace && !GenericAddressSpace) return false;
        if (req.Int64Atomics && !Int64Atomics) return false;
        if (req.SubGroups && !SubGroups) return false;
        return true;
    }

    /// <inheritdoc/>
    public override void CheckCompatibility(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not CLAcceleratorCapabilities req)
            throw new CapabilityNotSupportedException(
                $"Kernel targets {kernelRequirements.AcceleratorType} but device is OpenCL.");

        CheckFlag(nameof(Float16),             Float16,             req.Float16);
        CheckFlag(nameof(Float64),             Float64,             req.Float64);
        CheckFlag(nameof(GenericAddressSpace), GenericAddressSpace, req.GenericAddressSpace);
        CheckFlag(nameof(Int64Atomics),        Int64Atomics,        req.Int64Atomics);
        CheckFlag(nameof(SubGroups),           SubGroups,           req.SubGroups);
    }

    #endregion
}
