// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaAcceleratorCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Describes the capabilities of a CUDA accelerator, derived from SM architecture.
/// </summary>
public record class CudaAcceleratorCapabilities : AcceleratorCapabilities
{
    #region Static

    /// <summary>
    /// Derives all capability flags from a known SM architecture.
    /// </summary>
    /// <param name="arch">The CUDA SM architecture.</param>
    /// <returns>The capability set for the given architecture.</returns>
    public static CudaAcceleratorCapabilities FromArchitecture(AcceleratorArchitecture arch) =>
        new()
        {
            Architecture              = arch,
            Float16                   = arch >= CudaArchitecture.SM_53,
            Float64                   = true,
            BFloat16                  = arch >= CudaArchitecture.SM_80,
            Float16Min                = arch >= CudaArchitecture.SM_75,
            Float16Max                = arch >= CudaArchitecture.SM_80,
            Float16Tanh               = arch >= CudaArchitecture.SM_80,
            Float32Tanh               = arch >= CudaArchitecture.SM_75,
            TensorCores               = arch >= CudaArchitecture.SM_70,
            TensorCoresBF16           = arch >= CudaArchitecture.SM_80,
            TensorCoresFP8            = arch >= CudaArchitecture.SM_89,
            FP8                       = arch >= CudaArchitecture.SM_89,
            FP4                       = arch >= new AcceleratorArchitecture(10, 0),
            WarpShuffle               = arch >= CudaArchitecture.SM_30,
            DynamicParallelism        = arch >= CudaArchitecture.SM_35,
            CooperativeGroups         = arch >= CudaArchitecture.SM_60,
            ThreadBlockClusters       = arch >= CudaArchitecture.SM_90,
            Int8Dot                   = arch >= CudaArchitecture.SM_61,
            Int4Dot                   = arch >= CudaArchitecture.SM_75,
            MaxSharedMemoryPerBlockBytes = ComputeMaxSharedMemory(arch),
        };

    private static int ComputeMaxSharedMemory(AcceleratorArchitecture arch)
    {
        if (arch >= CudaArchitecture.SM_90) return 228 * 1024;
        if (arch >= CudaArchitecture.SM_80) return 164 * 1024;
        if (arch >= CudaArchitecture.SM_70) return  96 * 1024;
        return 48 * 1024;
    }

    private static void CheckFlag(string name, bool device, bool required)
    {
        if (required && !device)
            throw new CapabilityNotSupportedException(
                $"Kernel requires CUDA capability '{name}' which is not available on " +
                $"this device.");
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.Cuda;

    /// <summary>
    /// Returns the SM architecture.
    /// </summary>
    public AcceleratorArchitecture Architecture { get; init; }

    // --- Precision ---

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

    // --- Float16 intrinsics ---

    /// <summary>
    /// Returns true if the Float16 intrinsic Min is supported (SM_75+).
    /// </summary>
    public bool Float16Min { get; init; }

    /// <summary>
    /// Returns true if the Float16 intrinsic Max is supported (SM_80+).
    /// </summary>
    public bool Float16Max { get; init; }

    /// <summary>
    /// Returns true if the Float16 intrinsic TanH is supported (SM_80+).
    /// </summary>
    public bool Float16Tanh { get; init; }

    /// <summary>
    /// Returns true if the Float32 intrinsic TanH is supported (SM_75+).
    /// </summary>
    public bool Float32Tanh { get; init; }

    // --- Tensor / Matrix ---

    /// <summary>
    /// Returns true if Tensor Cores (mma instructions) are supported (SM_70+).
    /// </summary>
    public bool TensorCores { get; init; }

    /// <summary>
    /// Returns true if BF16 Tensor Cores are supported (SM_80+).
    /// </summary>
    public bool TensorCoresBF16 { get; init; }

    /// <summary>
    /// Returns true if FP8 Tensor Cores are supported (SM_89+).
    /// </summary>
    public bool TensorCoresFP8 { get; init; }

    // --- Compute features ---

    /// <summary>
    /// Returns true if warp shuffle instructions are supported (SM_30+).
    /// </summary>
    public bool WarpShuffle { get; init; }

    /// <summary>
    /// Returns true if dynamic parallelism is supported (SM_35+).
    /// </summary>
    public bool DynamicParallelism { get; init; }

    /// <summary>
    /// Returns true if cooperative groups are supported (SM_60+).
    /// </summary>
    public bool CooperativeGroups { get; init; }

    /// <summary>
    /// Returns true if thread block clusters are supported (SM_90+).
    /// </summary>
    public bool ThreadBlockClusters { get; init; }

    // --- Integer dot products ---

    /// <summary>
    /// Returns true if INT8 dot product instructions are supported (SM_61+).
    /// </summary>
    public bool Int8Dot { get; init; }

    /// <summary>
    /// Returns true if INT4 dot product instructions are supported (SM_75+).
    /// </summary>
    public bool Int4Dot { get; init; }

    // --- Shared memory ---

    /// <summary>
    /// Returns the maximum shared memory per block in bytes.
    /// </summary>
    public int MaxSharedMemoryPerBlockBytes { get; init; }

    #endregion

    #region AcceleratorCapabilities

    /// <inheritdoc/>
    public override int SpecificityOrdinal =>
        Architecture.Major * 10 + Architecture.Minor;

    /// <inheritdoc/>
    public override bool IsCompatible(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not CudaAcceleratorCapabilities req)
            return false;
        if (Architecture < req.Architecture)
            return false;
        if (req.BFloat16 && !BFloat16) return false;
        if (req.FP8 && !FP8) return false;
        if (req.FP4 && !FP4) return false;
        if (req.TensorCores && !TensorCores) return false;
        if (req.TensorCoresBF16 && !TensorCoresBF16) return false;
        if (req.TensorCoresFP8 && !TensorCoresFP8) return false;
        if (req.ThreadBlockClusters && !ThreadBlockClusters) return false;
        if (req.CooperativeGroups && !CooperativeGroups) return false;
        if (req.Int8Dot && !Int8Dot) return false;
        if (req.Int4Dot && !Int4Dot) return false;
        return true;
    }

    /// <inheritdoc/>
    public override void CheckCompatibility(AcceleratorCapabilities kernelRequirements)
    {
        if (kernelRequirements is not CudaAcceleratorCapabilities req)
            throw new CapabilityNotSupportedException(
                $"Kernel targets {kernelRequirements.AcceleratorType} but device is CUDA.");

        if (Architecture < req.Architecture)
            throw new CapabilityNotSupportedException(
                $"Kernel requires SM {req.Architecture} but device is SM {Architecture}.");

        CheckFlag(nameof(BFloat16),           BFloat16,           req.BFloat16);
        CheckFlag(nameof(FP8),                FP8,                req.FP8);
        CheckFlag(nameof(FP4),                FP4,                req.FP4);
        CheckFlag(nameof(TensorCores),         TensorCores,         req.TensorCores);
        CheckFlag(nameof(TensorCoresBF16),     TensorCoresBF16,     req.TensorCoresBF16);
        CheckFlag(nameof(TensorCoresFP8),      TensorCoresFP8,      req.TensorCoresFP8);
        CheckFlag(nameof(ThreadBlockClusters), ThreadBlockClusters, req.ThreadBlockClusters);
        CheckFlag(nameof(CooperativeGroups),   CooperativeGroups,   req.CooperativeGroups);
        CheckFlag(nameof(Int8Dot),             Int8Dot,             req.Int8Dot);
        CheckFlag(nameof(Int4Dot),             Int4Dot,             req.Int4Dot);
    }

    #endregion
}
