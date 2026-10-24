// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationProperties.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;

namespace ILGPUC;

// These types will be consumed by external test frameworks
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// The inlining behavior.
/// </summary>
public enum InliningMode
{
    /// <summary>
    /// Enables aggressive function inlining that inlines all functions by default.
    /// </summary>
    Aggressive = 0,

    /// <summary>
    /// Enables basic inlining heuristics and disables aggressive inlining
    /// behavior to reduce the overall code size.
    /// </summary>
    Conservative,
}

/// <summary>
/// Specifies the debug mode to use.
/// </summary>

public enum DebugSymbolsMode
{
    /// <summary>
    /// No debug symbols in kernels.
    /// </summary>
    None,

    /// <summary>
    /// Enables debug information in kernels (if available).
    /// </summary>
    Default,
}

/// <summary>
/// Represent an optimization level.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// Lightweight (required) transformations only.
    /// </summary>
    O0 = 0,

    /// <summary>
    /// Default release mode transformations.
    /// </summary>
    O1,

    /// <summary>
    /// Expensive transformations.
    /// </summary>
    O2,
}

/// <summary>
/// The math precision mode.
/// </summary>
public enum MathMode
{
    /// <summary>
    /// All floating point operations are performed using their intended bitness.
    /// </summary>
    /// <remarks>
    /// This is the default setting.
    /// </remarks>
    Default,

    /// <summary>
    /// Use fast math functions that are less precise.
    /// </summary>
    /// <remarks>
    /// On Metal, this mode enables the fast math of the Metal compiler: half and
    /// float divisions are approximated and the fast variants of the math
    /// functions are used.
    /// </remarks>
    Fast,

    /// <summary>
    /// Forces the use of 32-bit floats instead of 64-bit floats. This affects
    /// all math operations (like Math.Sqrt(double)) and all 64-bit float
    /// conversions. This settings might improve performance dramatically but
    /// might cause precision loss.
    /// </summary>
    Fast32BitOnly
}

/// <summary>
/// Specifies whether floating-point multiplications and additions may be fused
/// into multiply-adds.
/// </summary>
/// <remarks>
/// A fused multiply-add rounds once instead of twice, so expressions like
/// <c>a * b - c * d</c> may differ from the host in the last bit, and the NaN of
/// an infinite difference may become an infinity. CPU kernels are C# code that
/// the .NET JIT compiles without ever fusing multiplications and additions, so
/// they match the host in both modes.
/// </remarks>
public enum FusedMultiplyAddMode
{
    /// <summary>
    /// The native compilers may fuse multiplications and additions into
    /// multiply-adds on all GPU backends for the best performance.
    /// </summary>
    /// <remarks>
    /// This is the default setting (<c>--fma Fast</c>).
    /// </remarks>
    Fast,

    /// <summary>
    /// Multiplications and additions are never fused into multiply-adds, so that
    /// every operation rounds like it does on the host.
    /// </summary>
    /// <remarks>
    /// This mode (<c>--fma Off</c>) passes <c>--fmad=false</c> to nvcc on
    /// CUDA and <c>-ffp-contract=off</c> to hipcc on ROCm and to the Metal
    /// compiler. OpenCL and Metal sources start with
    /// <c>#pragma OPENCL FP_CONTRACT OFF</c> and
    /// <c>#pragma STDC FP_CONTRACT OFF</c>, which also apply when the runtime
    /// compiles the embedded source. Unless a fast math mode is requested, Metal
    /// kernels additionally divide half values by constants with correct
    /// rounding instead of multiplying with the reciprocal of the constant.
    /// </remarks>
    Off,
}

/// <summary>
/// Internal flags to specify the behavior in the presence of static fields.
/// </summary>
public enum StaticFieldMode
{
    /// <summary>
    /// Loads from readonly static fields are supported.
    /// </summary>
    /// <remarks>
    /// This is the default setting.
    /// </remarks>
    Default,

    /// <summary>
    /// Loads from mutable static fields are rejected by default.
    /// However, their current values can be inlined during JIT
    /// compilation. Adding this flags causes values from mutable
    /// static fields to be inlined instead of rejected.
    /// </summary>
    MutableStaticFields,

    /// <summary>
    /// Stores to static fields are rejected by default.
    /// Adding this flag causes stores to static fields
    /// to be silently ignored instead of rejected.
    /// </summary>
    IgnoreStaticFieldStores
}

/// <summary>
/// Internal flags to specify the behavior in the presence of static arrays. Note
/// that static array fields are also affected by the <see cref="StaticFieldMode"/>
/// settings.
/// </summary>
public enum ArrayMode
{
    /// <summary>
    /// Loads from static array values are rejected by default.
    /// </summary>
    Default,

    /// <summary>
    /// Loads from static arrays are supported and realized by inlining static
    /// array values.
    /// </summary>
    InlineMutableStaticArrays,
}

/// <summary>
/// Defines global context specific properties.
/// </summary>
/// <param name="DebugSymbolsMode">Debug symbols mode to use.</param>
/// <param name="EnableAssertions">True if assertions are enabled.</param>
/// <param name="EnableIOOperations">True if IO operations are enabled.</param>
/// <param name="EnableMathFlushToZero">
/// True if denorm floats should be flushed to zero.
/// </param>
/// <param name="OptimizationLevel">Optimization level to use.</param>
/// <param name="InliningMode">Inlining mode to use.</param>
/// <param name="MathMode">Math mode to use.</param>
/// <param name="FusedMultiplyAddMode">
/// Specifies whether multiplications and additions may be fused into
/// multiply-adds.
/// </param>
/// <param name="StaticFieldMode">Static field mode to use.</param>
/// <param name="ArrayMode">Array mode to use.</param>
/// <param name="ConstantEvaluationBudget">
/// The number of IL instructions the frontend may interpret to evaluate one call of
/// a helper predicate (such as <c>IsFloat&lt;T&gt;()</c>) at compile time. Calls
/// that need more are compiled as calls.
/// </param>
public sealed record class CompilationProperties(
    DebugSymbolsMode DebugSymbolsMode = DebugSymbolsMode.Default,
    bool EnableAssertions = true,
    bool EnableIOOperations = true,
    bool EnableMathFlushToZero = false,
    OptimizationLevel OptimizationLevel = OptimizationLevel.O1,
    InliningMode InliningMode = InliningMode.Aggressive,
    MathMode MathMode = MathMode.Default,
    FusedMultiplyAddMode FusedMultiplyAddMode = FusedMultiplyAddMode.Fast,
    StaticFieldMode StaticFieldMode = StaticFieldMode.Default,
    ArrayMode ArrayMode = ArrayMode.Default,
    int ConstantEvaluationBudget = CompilationProperties.DefaultConstantEvaluationBudget)
{
    /// <summary>
    /// The default of <see cref="ConstantEvaluationBudget"/>.
    /// </summary>
    public const int DefaultConstantEvaluationBudget = 100_000;

    /// <summary>
    /// Represents the target platform to use.
    /// </summary>
    public TargetPlatform TargetPlatform { get; } = TargetPlatform.Platform64Bit;
}

#pragma warning restore CA1515
