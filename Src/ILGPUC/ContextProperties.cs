// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ContextProperties.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPUC;

/// <summary>
/// The inlining behavior.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1027:Mark enums with FlagsAttribute",
    Justification = "This no flags enumeration")]
[SuppressMessage(
    "Design",
    "CA1069:Enums values should not be duplicated",
    Justification = "This is required to be backwards compatible")]
enum InliningMode
{
    /// <summary>
    /// All functions will be inlined by default.
    /// </summary>
    /// <remarks>
    /// This is the default setting.
    /// </remarks>
    Default = Aggressive,

    /// <summary>
    /// Enables aggressive function inlining that inlines all functions by default.
    /// </summary>
    Aggressive = 0,

    /// <summary>
    /// Enables basic inlining heuristics and disables aggressive inlining
    /// behavior to reduce the overall code size.
    /// </summary>
    Conservative,

    /// <summary>
    /// No functions will be inlined at all.
    /// </summary>
    [Obsolete("Disabling inlining is no longer supported")]
    Disabled,
}

/// <summary>
/// Specifies the debug mode to use.
/// </summary>
enum DebugSymbolsMode
{
    /// <summary>
    /// Automatic decision on debug symbols. If a debugger is attached, this mode
    /// changes to <see cref="Basic"/>. If not, all debug symbols will be
    /// <see cref="Disabled"/>.
    /// </summary>
    /// <remarks>
    /// This is the default setting.
    /// </remarks>
    Auto,

    /// <summary>
    /// No debug symbols will be loaded.
    /// </summary>
    Disabled,

    /// <summary>
    /// Debug information loaded from portable PDBs to enhance error messages
    /// and assertion checks.
    /// </summary>
    Basic,

    /// <summary>
    /// Enables debug information in kernels (if available).
    /// </summary>
    Kernel,

    /// <summary>
    /// Enabled source-code annotations in generated kernels (implies
    /// <see cref="Kernel"/>).
    /// </summary>
    KernelSourceAnnotations,
}

/// <summary>
/// Represent an optimization level.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1027:Mark enums with FlagsAttribute",
    Justification = "This no flags enumeration")]
[SuppressMessage(
    "Design",
    "CA1069:Enums values should not be duplicated",
    Justification = "This is required to be backwards compatible")]
enum OptimizationLevel
{
    /// <summary>
    /// Defaults to O0.
    /// </summary>
    Debug = O0,

    /// <summary>
    /// Defaults to O1.
    /// </summary>
    /// <remarks>
    /// This is the default setting for new <see cref="Context"/> instances.
    /// </remarks>
    Release = O1,

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
class CodeGenerationProperties
{
    #region Properties

    /// <summary>
    /// Returns the current debug symbols mode.
    /// </summary>
    /// <remarks><see cref="DebugSymbolsMode.Auto"/> by default.</remarks>
    public DebugSymbolsMode DebugSymbolsMode { get; protected set; } =
        DebugSymbolsMode.Auto;

    /// <summary>
    /// Returns true if the optimized kernels should be debugged.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool ForceDebuggingOfOptimizedKernels { get; protected set; }

    /// <summary>
    /// Returns true if the internal IR verifier is enabled.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableVerifier { get; protected set; }

    /// <summary>
    /// Returns true if assertions are enabled.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableAssertions { get; protected set; }

    /// <summary>
    /// Returns true if IO is enabled.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableIOOperations { get; protected set; }

    /// <summary>
    /// Returns true if additional kernel information is enabled.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableKernelInformation { get; protected set; }

    /// <summary>
    /// Returns true if multiple threads should be used to generate code for
    /// different methods in parallel.
    /// </summary>
    /// <remarks>Disabled by default.</remarks>
    public bool EnableParallelCodeGenerationInFrontend { get; private set; }

    /// <summary>
    /// The current optimization level to use.
    /// </summary>
    /// <remarks><see cref="OptimizationLevel.Release"/> by default.</remarks>
    public OptimizationLevel OptimizationLevel { get; protected set; } =
        OptimizationLevel.Release;

    /// <summary>
    /// The current inlining mode to use.
    /// </summary>
    /// <remarks><see cref="InliningMode.Default"/> by default.</remarks>
    public InliningMode InliningMode { get; protected set; } = InliningMode.Default;

    /// <summary>
    /// The current math mode.
    /// </summary>
    /// <remarks><see cref="MathMode.Default"/> by default.</remarks>
    public MathMode MathMode { get; protected set; } = MathMode.Default;

    /// <summary>
    /// Defines how to deal with static fields.
    /// </summary>
    /// <remarks><see cref="MathMode.Default"/> by default.</remarks>
    public StaticFieldMode StaticFieldMode { get; protected set; } =
        StaticFieldMode.Default;

    /// <summary>
    /// Defines how to deal with arrays.
    /// </summary>
    /// <remarks><see cref="ArrayMode.Default"/> by default.</remarks>
    public ArrayMode ArrayMode { get; protected set; } =
        ArrayMode.Default;

    /// <summary>
    /// Returns the path to LibNVVM DLL.
    /// </summary>
    public string? LibNvvmPath { get; protected set; }

    /// <summary>
    /// Returns the path to LibDevice bitcode.
    /// </summary>
    public string? LibDevicePath { get; protected set; }

    #endregion
}
