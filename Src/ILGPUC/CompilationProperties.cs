// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationProperties.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;

namespace ILGPUC;

/// <summary>
/// The inlining behavior.
/// </summary>
enum InliningMode
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
enum DebugSymbolsMode
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
enum OptimizationLevel
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
enum MathMode
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
/// Internal flags to specify the behavior in the presence of static fields.
/// </summary>
enum StaticFieldMode
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
enum ArrayMode
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
/// <param name="StaticFieldMode">Static field mode to use.</param>
/// <param name="ArrayMode">Array mode to use.</param>
sealed record class CompilationProperties(
    DebugSymbolsMode DebugSymbolsMode = DebugSymbolsMode.Default,
    bool EnableAssertions = true,
    bool EnableIOOperations = true,
    bool EnableMathFlushToZero = false,
    OptimizationLevel OptimizationLevel = OptimizationLevel.O1,
    InliningMode InliningMode = InliningMode.Aggressive,
    MathMode MathMode = MathMode.Default,
    StaticFieldMode StaticFieldMode = StaticFieldMode.Default,
    ArrayMode ArrayMode = ArrayMode.Default)
{
    /// <summary>
    /// Represents the target platform to use.
    /// </summary>
    public TargetPlatform TargetPlatform { get; } = TargetPlatform.Platform64Bit;
}
