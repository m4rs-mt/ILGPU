// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmAcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Globalization;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Identifies a ROCm GPU architecture family.
/// </summary>
public enum ROCmArchitectureFamily
{
    /// <summary>
    /// GCN (legacy) architecture.
    /// </summary>
    GCN,

    /// <summary>
    /// CDNA architecture (data center, MI series).
    /// </summary>
    CDNA,

    /// <summary>
    /// RDNA architecture (gaming, gfx10xx+).
    /// </summary>
    RDNA,
}

/// <summary>
/// Describes the capabilities of a ROCm/HIP accelerator, derived from the gfx
/// architecture string (e.g., "gfx1100").
/// </summary>
public record class ROCmAcceleratorCapabilities : AcceleratorCapabilities
{
    #region Static

    /// <summary>
    /// Derives all capability flags from a ROCm architecture string such as "gfx1100".
    /// </summary>
    /// <param name="arch">The architecture string reported by the HIP runtime.</param>
    /// <returns>The capability set for the given architecture.</returns>
    public static ROCmAcceleratorCapabilities FromArchitectureString(string arch)
    {
        if (string.IsNullOrEmpty(arch) ||
            !arch.StartsWith("gfx", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(
                arch.AsSpan(3),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int gfxNum))
        {
            // Unknown / unparseable architecture — return minimal safe defaults.
            return new ROCmAcceleratorCapabilities
            {
                ArchitectureString = arch ?? string.Empty,
                ArchitectureFamily = ROCmArchitectureFamily.GCN,
                WavefrontSize = 64,
                SupportsWave32 = false,
                SupportsWave64 = true,
                Float16 = true,
                Float64 = false,
                BFloat16 = false,
                FP8 = false,
                FP4 = false,
                CDNAMatrixCores = false,
                RDNAMatrixCores = false,
                PackedMath = false,
            };
        }

        return BuildFromGfxNum(arch, gfxNum);
    }

    /// <summary>
    /// Builds ROCm capabilities from an architecture string and model number.
    /// </summary>
    /// <param name="arch">The architecture string.</param>
    /// <param name="gfxNum">The model number.</param>
    /// <returns>The created capabilities.</returns>
    private static ROCmAcceleratorCapabilities BuildFromGfxNum(string arch, int gfxNum)
    {
        // gfx9xx  (0x900–0x9FF) — GCN / CDNA
        if (gfxNum >= 0x900 && gfxNum < 0xA00)
        {
            bool isCDNA = gfxNum >= 0x908;  // gfx908+ is CDNA (MI100+)
            bool hasBF16 = gfxNum >= 0x90A;  // gfx90a+ (MI200) has BFloat16
            return new ROCmAcceleratorCapabilities
            {
                ArchitectureString = arch,
                ArchitectureFamily = isCDNA
                    ? ROCmArchitectureFamily.CDNA
                    : ROCmArchitectureFamily.GCN,
                WavefrontSize = 64,
                SupportsWave32 = false,
                SupportsWave64 = true,
                Float16 = true,
                Float64 = true,
                BFloat16 = hasBF16,
                FP8 = false,
                FP4 = false,
                CDNAMatrixCores = isCDNA,
                RDNAMatrixCores = false,
                PackedMath = isCDNA,
            };
        }

        // gfx11xx (0x1100–0x11FF) — RDNA3 (RX 7000 series): wmma support
        if (gfxNum >= 0x1100 && gfxNum < 0x1200)
        {
            return new ROCmAcceleratorCapabilities
            {
                ArchitectureString = arch,
                ArchitectureFamily = ROCmArchitectureFamily.RDNA,
                WavefrontSize = 32,
                SupportsWave32 = true,
                SupportsWave64 = false,
                Float16 = true,
                Float64 = false,
                BFloat16 = false,
                FP8 = false,
                FP4 = false,
                CDNAMatrixCores = false,
                RDNAMatrixCores = true,
                PackedMath = true,
            };
        }

        // gfx12xx (0x1200+) — RDNA4 (RX 9000 series): wmma support
        if (gfxNum >= 0x1200)
        {
            return new ROCmAcceleratorCapabilities
            {
                ArchitectureString = arch,
                ArchitectureFamily = ROCmArchitectureFamily.RDNA,
                WavefrontSize = 32,
                SupportsWave32 = true,
                SupportsWave64 = false,
                Float16 = true,
                Float64 = false,
                BFloat16 = false,
                FP8 = false,
                FP4 = false,
                CDNAMatrixCores = false,
                RDNAMatrixCores = true,
                PackedMath = true,
            };
        }

        // gfx10xx (0x1000–0x10FF) — RDNA1/2: no matrix cores
        return new ROCmAcceleratorCapabilities
        {
            ArchitectureString = arch,
            ArchitectureFamily = ROCmArchitectureFamily.RDNA,
            WavefrontSize = 32,
            SupportsWave32 = true,
            SupportsWave64 = false,
            Float16 = true,
            Float64 = false,
            BFloat16 = false,
            FP8 = false,
            FP4 = false,
            CDNAMatrixCores = false,
            RDNAMatrixCores = false,
            PackedMath = true,
        };
    }

    /// <summary>
    /// Checks device and whether it is required or not.
    /// </summary>
    private static void CheckFlag(string name, bool device, bool required)
    {
        if (required && !device)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel requires ROCm capability '{name}' which is not available on " +
                $"this device.");
        }
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.ROCm;

    /// <summary>
    /// Returns the raw architecture string (e.g., "gfx1100").
    /// </summary>
    public string ArchitectureString { get; init; } = string.Empty;

    /// <summary>
    /// Returns the GPU architecture family.
    /// </summary>
    public ROCmArchitectureFamily ArchitectureFamily { get; init; }

    /// <summary>
    /// Returns the native wavefront size (32 for RDNA, 64 for CDNA/GCN).
    /// </summary>
    public int WavefrontSize { get; init; }

    /// <summary>
    /// Returns true if wave32 execution mode is supported.
    /// </summary>
    public bool SupportsWave32 { get; init; }

    /// <summary>
    /// Returns true if wave64 execution mode is supported.
    /// </summary>
    public bool SupportsWave64 { get; init; }

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
    /// Returns true if CDNA mfma matrix core instructions are supported.
    /// </summary>
    public bool CDNAMatrixCores { get; init; }

    /// <summary>
    /// Returns true if RDNA wmma matrix instructions are supported (RDNA3+).
    /// </summary>
    public bool RDNAMatrixCores { get; init; }

    /// <summary>
    /// Returns true if packed v_pk_* math instructions are supported.
    /// </summary>
    public bool PackedMath { get; init; }

    #endregion

    #region AcceleratorCapabilities

    /// <inheritdoc/>
    public override int SpecificityOrdinal
    {
        get
        {
            // Parse gfx number from architecture string for ordering
            if (ArchitectureString.Length > 3 &&
                int.TryParse(
                    ArchitectureString.AsSpan(3),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int gfxNum))
                return gfxNum;
            return 0;
        }
    }

    /// <inheritdoc/>
    public override bool IsCompatible(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not ROCmAcceleratorCapabilities req)
            return false;
        if (ArchitectureFamily != req.ArchitectureFamily && req.CDNAMatrixCores)
        {
            if (req.CDNAMatrixCores && !CDNAMatrixCores) return false;
        }
        if (req.WavefrontSize != 0 && WavefrontSize != req.WavefrontSize)
            return false;
        if (req.Float64 && !Float64) return false;
        if (req.BFloat16 && !BFloat16) return false;
        if (req.FP8 && !FP8) return false;
        if (req.FP4 && !FP4) return false;
        if (req.CDNAMatrixCores && !CDNAMatrixCores) return false;
        if (req.RDNAMatrixCores && !RDNAMatrixCores) return false;
        return true;
    }

    /// <inheritdoc/>
    public override void CheckCompatibility(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not ROCmAcceleratorCapabilities req)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel targets {kernelRequirements.AcceleratorType}"
                + " but device is ROCm.");
        }

        if (ArchitectureFamily != req.ArchitectureFamily && req.CDNAMatrixCores)
            CheckFlag(nameof(CDNAMatrixCores), CDNAMatrixCores, req.CDNAMatrixCores);

        if (req.WavefrontSize != 0 && WavefrontSize != req.WavefrontSize)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel requires wavefront size {req.WavefrontSize} but device"
                + " has {WavefrontSize}.");
        }

        CheckFlag(nameof(Float64), Float64, req.Float64);
        CheckFlag(nameof(BFloat16), BFloat16, req.BFloat16);
        CheckFlag(nameof(FP8), FP8, req.FP8);
        CheckFlag(nameof(FP4), FP4, req.FP4);
        CheckFlag(nameof(CDNAMatrixCores), CDNAMatrixCores, req.CDNAMatrixCores);
        CheckFlag(nameof(RDNAMatrixCores), RDNAMatrixCores, req.RDNAMatrixCores);
    }

    #endregion
}
