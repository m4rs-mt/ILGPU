// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// CPU backend for generating vectorized C# code that simulates GPU execution.
/// </summary>
/// <remarks>
/// This backend transforms GPU kernels into Tensor&lt;T&gt;-based vectorized C# code for CPU
/// execution. Vectorized values are represented as Tensor&lt;T&gt;, with math operations
/// delegated to CPUMathIntrinsics (backed by TensorPrimitives for SIMD acceleration).
/// </remarks>
/// <param name="vectorWidth">The vector width (number of lanes).</param>
/// <param name="emitDebugSymbols">
/// Whether to emit #line directives for debugging support.
/// </param>
/// <param name="poolBuffers">
/// Whether to pre-allocate vector buffers outside the kernel and pass them
/// as parameters. Enabled by default; disable for easier debugging.
/// </param>
sealed class CPUBackend(
    int vectorWidth = 8,
    bool emitDebugSymbols = false,
    bool poolBuffers = true) :
    Backend(
        BackendType.CPU,
        AcceleratorType.CPU,
        CPUAcceleratorCapabilities.Default)
{
    /// <summary>
    /// Returns the vector width (number of SIMD lanes).
    /// </summary>
    public int VectorWidth { get; } = vectorWidth;

    /// <summary>
    /// Returns whether to emit debug symbols (#line directives).
    /// </summary>
    public bool EmitDebugSymbols { get; } = emitDebugSymbols;

    /// <summary>
    /// Returns whether to pre-allocate vector buffers outside the kernel.
    /// </summary>
    public bool PoolBuffers { get; } = poolBuffers;

    /// <summary>
    /// Returns the current warp size (vector width for CPU backend).
    /// </summary>
    public override int? CurrentWarpSize => VectorWidth;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public override LanguageConfiguration LanguageConfiguration { get; } =
        new CPULanguageConfiguration(vectorWidth);

    /// <inheritdoc/>
    public override LauncherEmitter CreateLauncherEmitter() =>
        new CPULauncherEmitter();

    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new CPUCompiledKernelEmitter(VectorWidth, "KernelClass");

    /// <inheritdoc/>
    protected override ArchitectureSpecification GetArchitectureSpecification() =>
        new(Capabilities, CurrentWarpSize, new(1, 0), SupportsViews: true);

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides code generation to use vectorization-aware C# code generator.
    /// </remarks>
    protected override CodeGenerationResult GenerateCode(Module module)
    {
        var codeGenerator = new CPUCodeGenerator(
            module,
            LanguageConfiguration,
            VectorWidth,
            EmitDebugSymbols,
            PoolBuffers);
        return codeGenerator.GenerateCode();
    }
}
