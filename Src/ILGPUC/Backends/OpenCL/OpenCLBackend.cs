// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using ILGPUC.IR.Transformations;

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// Represents an ILGPU backend for OpenCL.
/// </summary>
class OpenCLBackend(
    int clcMajor,
    int clcMinor,
    CLVendor vendor = CLVendor.Intel,
    int? warpSize = null) :
    Backend(BackendType.OpenCL, AcceleratorType.OpenCL, new CLAcceleratorCapabilities())
{
    /// <summary>
    /// Returns the warp size (sub_group_size) for this OpenCL target.
    /// </summary>
    public override int? CurrentWarpSize => warpSize;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public override LanguageConfiguration LanguageConfiguration { get; } =
        new CLLanguageConfiguration(vendor);

    /// <inheritdoc/>
    public override LauncherEmitter CreateLauncherEmitter() =>
        new OpenCLLauncherEmitter();

    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new OpenCLCompiledKernelEmitter(clcMajor, clcMinor);

    /// <inheritdoc/>
    protected override ArchitectureSpecification GetArchitectureSpecification() =>
        new(Capabilities, CurrentWarpSize, new(0, 0), SupportsViews: false);
}
