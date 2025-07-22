// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalAcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Describes the capabilities of a Metal (Apple Silicon) accelerator.
/// </summary>
public record class MetalAcceleratorCapabilities : AcceleratorCapabilities
{
    #region Static

    /// <summary>
    /// Derives all capability flags from a Metal GPU family.
    /// </summary>
    /// <param name="gpuFamily">The Metal GPU family.</param>
    /// <returns>The capability set for the given GPU family.</returns>
    public static MetalAcceleratorCapabilities FromGPUFamily(MetalGPUFamily gpuFamily) =>
        new()
        {
            GPUFamily = gpuFamily,
            Float16 = true,   // All Apple Silicon supports Float16
            Float64 = false,  // Metal does not support double precision
            RayTracing = gpuFamily >= MetalGPUFamily.Apple7,
            MeshShaders = gpuFamily >= MetalGPUFamily.Apple9,
            SparseTextures = gpuFamily >= MetalGPUFamily.Apple8,
            DynamicLibraries = gpuFamily >= MetalGPUFamily.Apple7,
            ArgumentBuffersTier2 = gpuFamily >= MetalGPUFamily.Apple7,
        };

    private static void CheckFlag(string name, bool device, bool required)
    {
        if (required && !device)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel requires Metal capability '{name}' which is not available on " +
                $"this device.");
        }
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.Metal;

    /// <summary>
    /// Returns the target GPU family.
    /// </summary>
    public MetalGPUFamily GPUFamily { get; init; }

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
    /// Returns true if ray tracing is supported (Apple7+).
    /// </summary>
    public bool RayTracing { get; init; }

    /// <summary>
    /// Returns true if mesh shaders are supported (Apple9+).
    /// </summary>
    public bool MeshShaders { get; init; }

    /// <summary>
    /// Returns true if sparse textures are supported (Apple8+).
    /// </summary>
    public bool SparseTextures { get; init; }

    /// <summary>
    /// Returns true if dynamic libraries are supported (Apple7+).
    /// </summary>
    public bool DynamicLibraries { get; init; }

    /// <summary>
    /// Returns true if argument buffers tier 2 are supported (Apple7+).
    /// </summary>
    public bool ArgumentBuffersTier2 { get; init; }

    #endregion

    #region AcceleratorCapabilities

    /// <inheritdoc/>
    public override int SpecificityOrdinal => (int)GPUFamily;

    /// <inheritdoc/>
    public override bool IsCompatible(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not MetalAcceleratorCapabilities req)
            return false;
        if (GPUFamily < req.GPUFamily)
            return false;
        if (req.RayTracing && !RayTracing) return false;
        if (req.MeshShaders && !MeshShaders) return false;
        if (req.SparseTextures && !SparseTextures) return false;
        if (req.DynamicLibraries && !DynamicLibraries) return false;
        if (req.ArgumentBuffersTier2 && !ArgumentBuffersTier2) return false;
        return true;
    }

    /// <inheritdoc/>
    public override void CheckCompatibility(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not MetalAcceleratorCapabilities req)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel targets {kernelRequirements.AcceleratorType} but device"
                + " is Metal.");
        }

        if (GPUFamily < req.GPUFamily)
        {
            throw new CapabilityNotSupportedException(
                $"Kernel requires Metal GPU family {req.GPUFamily} but device is"
                + $" {GPUFamily}.");
        }

        CheckFlag(nameof(RayTracing), RayTracing, req.RayTracing);
        CheckFlag(nameof(MeshShaders), MeshShaders, req.MeshShaders);
        CheckFlag(nameof(SparseTextures), SparseTextures, req.SparseTextures);
        CheckFlag(nameof(DynamicLibraries), DynamicLibraries, req.DynamicLibraries);
        CheckFlag(
            nameof(ArgumentBuffersTier2),
            ArgumentBuffersTier2,
            req.ArgumentBuffersTier2);
    }

    #endregion
}
