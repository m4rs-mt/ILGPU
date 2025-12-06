// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmLanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends.Cuda;
using ILGPUC.IR;
using System.Collections.Generic;

namespace ILGPUC.Backends.ROCm;

/// <summary>
/// HIP-specific language configuration for AMD ROCm.
/// HIP (Heterogeneous-Compute Interface for Portability) is AMD's CUDA-compatible
/// API that allows portable GPU programming across NVIDIA and AMD hardware.
/// Most syntax and semantics are identical to CUDA, with minor differences in
/// built-in variable names (e.g., hipThreadIdx_x vs threadIdx.x).
/// </summary>
/// <remarks>
/// HIP uses the same keywords and address space qualifiers as CUDA:
/// - __global__ for kernel functions
/// - __shared__ for shared memory
/// - __device__ and __host__ function qualifiers
/// - Same atomic operations and synchronization primitives
///
/// Key differences from CUDA:
/// - Thread index built-ins use "hip" prefix: hipThreadIdx_x, hipBlockIdx_x, etc.
/// - Some ROCm-specific optimizations and intrinsics available
/// - Different runtime API (hipMalloc vs cudaMalloc, etc.) but kernel code is nearly
/// identical
/// </remarks>
sealed class ROCmLanguageConfiguration : CudaLanguageConfiguration
{
    /// <inheritdoc/>
    /// <remarks>
    /// HIP uses CUDA-compatible address space keywords.
    /// </remarks>
    public override string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace) =>
        base.GetAddressSpaceKeyword(addressSpace);

    /// <inheritdoc/>
    /// <remarks>
    /// HIP uses the same primitive type names as CUDA.
    /// </remarks>
    public override string GetPrimitiveTypeName(BasicValueType basicType) =>
        base.GetPrimitiveTypeName(basicType);

    /// <inheritdoc/>
    /// <remarks>
    /// HIP supports the same features as CUDA.
    /// </remarks>
    public override bool SupportsFeature(LanguageFeature feature) =>
        base.SupportsFeature(feature);

    /// <inheritdoc/>
    /// <remarks>
    /// The <c>hipcc</c> wrapper auto-includes <c>hip_runtime.h</c>, but
    /// the compiler service invokes <c>clang++ -x hip</c> directly so
    /// the header must be requested explicitly. Without it, none of HIP's
    /// kernel-side identifiers are declared (<c>__global__</c>'s macro
    /// expansion needs <c>hipLaunchKernel</c>, plus <c>hipThreadIdx_x</c>,
    /// <c>hipBlockIdx_x</c>, <c>hipBlockDim_x</c>, <c>hipGridDim_x</c>,
    /// <c>warpSize</c>, <c>__syncthreads</c>, <c>__lane_id</c>,
    /// <c>atomicAdd</c>, etc.) and every kernel fails to compile.
    /// </remarks>
    public override IEnumerable<string> GetHeaderIncludes()
    {
        yield return "#include <hip/hip_runtime.h>";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a HIP-specific intrinsic emitter that handles the differences
    /// in thread/block index syntax (hipThreadIdx_x vs threadIdx.x) while
    /// maintaining CUDA compatibility for all other intrinsics.
    /// </remarks>
    public override IntrinsicEmitter CreateIntrinsicEmitter() =>
        new ROCmIntrinsicEmitter();
}
