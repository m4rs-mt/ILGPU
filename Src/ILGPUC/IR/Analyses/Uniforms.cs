// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Uniforms.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// The state of a value.
/// </summary>
enum UniformKind
{
    /// <summary>
    /// No or insufficient information is available for the value attached.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The associated value can be considered uniform.
    /// </summary>
    Uniform = 1,

    /// <summary>
    /// The associated value has to be considered divergent.
    /// </summary>
    Divergent = 2,
}

/// <summary>
/// Information carried per value.
/// </summary>
/// <remarks>
/// Constructs a new value information instance.
/// </remarks>
/// <param name="Kind">The associated value kind.</param>
readonly record struct UniformValueInfo(UniformKind Kind)
{
    /// <summary>
    /// Returns the string representation of this instance.
    /// </summary>
    /// <returns>The string representation of this instance.</returns>
    public override string ToString() => Kind.ToString();

    /// <summary>
    /// Converts a value of type <see cref="UniformKind"/> into a
    /// <see cref="UniformValueInfo"/> instance.
    /// </summary>
    /// <param name="kind">The kind to convert to.</param>
    /// <returns>The created value information instance.</returns>
    public static implicit operator UniformValueInfo(UniformKind kind) => new(kind);
}

/// <summary>
/// An analysis to determine whether values and terminators can be considered uniform.
/// </summary>
/// <remarks>
/// Constructs a new analysis implementation.
/// </remarks>
sealed class Uniforms() :
    FixPointAnalysis<UniformValueInfo, Forwards>(defaultValue: UniformKind.Unknown)
{
    #region Nested Types

    /// <summary>
    /// Stores information of a uniform analysis run.
    /// </summary>
    /// <param name="result">The result mapping.</param>
    /// <remarks>
    /// Constructs a new uniform analysis result.
    /// </remarks>
    internal readonly struct Info(FixPointAnalysisResult<UniformValueInfo>? result)
    {
        /// <summary>
        /// Empty allocation information.
        /// </summary>
        public static readonly Info Empty = new(null);

        /// <summary>
        /// Returns uniform information for the given value.
        /// </summary>
        /// <param name="value">The value to get uniform information for.</param>
        /// <returns>The uniform kind (can be Unknown).</returns>
        public UniformKind this[Value value] =>
            result?.GetResult(value).Data.Kind ?? UniformKind.Unknown;

        /// <summary>
        /// Returns true if this uniform information object is empty.
        /// </summary>
        public bool IsEmpty => !result.HasValue;

        /// <summary>
        /// Returns true if the given value can be considered to be uniformly
        /// distributed across all threads in the current group. However, it
        /// pessimistically assumes that <see cref="UniformKind.Unknown"/> refers
        /// to a divergent value.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>True, if the given value can be considered uniform.</returns>
        public bool IsUniform(Value value) => this[value] == UniformKind.Uniform;
    }

    #endregion

    #region Analysis

    /// <summary>
    /// Creates a new uniforms analysis.
    /// </summary>
    public static Uniforms Create() => new();

    /// <summary>
    /// Applies a new uniform analysis to the given root method.
    /// </summary>
    /// <param name="entryPoint">The root (entry) method.</param>
    public static Info Apply(Method entryPoint)
    {
        var analysis = Create();
        var result = analysis.AnalyzeModule(entryPoint);
        return new Info(result);
    }

    /// <summary>
    /// Returns initial and unconstrained information about whether the given value
    /// can be considered uniform.
    /// </summary>
    /// <param name="node">The IR node.</param>
    /// <returns>The uniform state of the given node.</returns>
    private static UniformValueInfo GetInitialUniformKind(Value node) =>
        node switch
        {
            // Thread-dependent values are divergent by definition
            SubGroupLaneIndexValue _ => UniformKind.Divergent,
            SubGroupIndexValue _ => UniformKind.Divergent,
            GroupIndexValue _ => UniformKind.Divergent,
            // (Device-wide) constants are uniform by definition
            GridIndexValue _ => UniformKind.Uniform,
            PrimitiveValue => UniformKind.Uniform,
            NullValue => UniformKind.Uniform,
            UndefinedValue => UniformKind.Uniform,
            // Method calls can be considered uniform since each thread will perform
            // the same call (since we do not support virtual and jump-table based
            // calls at the moment)
            MethodCall _ => UniformKind.Uniform,
            // Phi values need special handling
            PhiValue => UniformKind.Unknown,
            // All remaining values have an unknown state
            _ => UniformKind.Unknown
        };

    /// <summary>
    /// Returns the maximum of the first and the second kind.
    /// </summary>
    protected override UniformValueInfo Merge(
        UniformValueInfo first,
        UniformValueInfo second) =>
        (UniformKind)Math.Max((int)first.Kind, (int)second.Kind);

    /// <summary>
    /// Analyzes a value to compute its uniform information.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override AnalysisValue<UniformValueInfo> Analyze(
        Value value,
        GlobalValueMap<AnalysisValue<UniformValueInfo>> data)
    {
        // Get current value or create initial one
        var current = data.TryGetValue(value, out var existing)
            ? existing
            : CreateInitialValue(value.Type);

        // Handle special cases that need custom analysis
        var specialized = value switch
        {
            // Phi values: merge all incoming values
            PhiValue phiValue => MergePhiValue(phiValue, data),

            // For method calls, use the method return value
            MethodCall call when !call.Target.IsVoid =>
                data.TryGetValue(call.Target, out var retValue)
                    ? retValue
                    : current,

            // Default: use initial uniform kind
            _ => AnalysisValue.Create(GetInitialUniformKind(value), value.Type)
        };

        return specialized;
    }

    #endregion
}
