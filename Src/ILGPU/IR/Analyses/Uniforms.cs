// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Uniforms.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to determine whether values and terminators can be considered uniform.
    /// </summary>
    public class Uniforms : GlobalFixPointAnalysis<Uniforms.ValueInfo, Forwards>
    {
        #region Nested Types

        /// <summary>
        /// The state of a value.
        /// </summary>
        public enum UniformKind
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
        public readonly struct ValueInfo : IEquatable<ValueInfo>
        {
            #region Instance

            /// <summary>
            /// Constructs a new value information instance.
            /// </summary>
            /// <param name="kind">The associated value kind.</param>
            public ValueInfo(UniformKind kind)
            {
                Kind = kind;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current kind of the current uniform information instance.
            /// </summary>
            public UniformKind Kind { get; }

            #endregion

            #region IEquatable

            /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>>
            public bool Equals(ValueInfo other) =>
                Kind == other.Kind;

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to the current instance.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>
            /// True, if the given object is equal to the current instance.
            /// </returns>
            public override bool Equals(object obj) =>
                obj is ValueInfo other && Equals(other);

            /// <summary>
            /// Returns the hash code of this instance.
            /// </summary>
            /// <returns>The hash code of this instance.</returns>
            public override int GetHashCode() => (int)Kind;

            /// <summary>
            /// Returns the string representation of this instance.
            /// </summary>
            /// <returns>The string representation of this instance.</returns>
            public override string ToString() => Kind.ToString();

            #endregion

            #region Operators

            /// <summary>
            /// Converts a value of type <see cref="UniformKind"/> into a
            /// <see cref="ValueInfo"/> instance.
            /// </summary>
            /// <param name="kind">The kind to convert to.</param>
            /// <returns>The created value information instance.</returns>
            public static implicit operator ValueInfo(UniformKind kind) =>
                new ValueInfo(kind);

            /// <summary>
            /// Returns true if the first and second information instances are the same.
            /// </summary>
            /// <param name="first">The first instance.</param>
            /// <param name="second">The second instance.</param>
            /// <returns>True, if the first and second instances are the same.</returns>
            public static bool operator ==(ValueInfo first, ValueInfo second) =>
                first.Equals(second);

            /// <summary>
            /// Returns true if the first and second information instances are not the
            /// same.
            /// </summary>
            /// <param name="first">The first instance.</param>
            /// <param name="second">The second instance.</param>
            /// <returns>
            /// True, if the first and second instances are not the same.
            /// </returns>
            public static bool operator !=(ValueInfo first, ValueInfo second) =>
                !first.Equals(second);

            #endregion
        }

        /// <summary>
        /// Stores information of a uniform analysis run.
        /// </summary>
        public readonly struct Info
        {
            #region Static

            /// <summary>
            /// Empty allocation information.
            /// </summary>
            public static readonly Info Empty =
                new Info(GlobalAnalysisValueResult<ValueInfo>.Empty);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new alignment analysis.
            /// </summary>
            internal Info(GlobalAnalysisValueResult<ValueInfo> analysisResult)
            {
                AnalysisResult = analysisResult;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Stores a method value-alignment mapping.
            /// </summary>
            public GlobalAnalysisValueResult<ValueInfo> AnalysisResult { get; }

            /// <summary>
            /// Returns pointer alignment information for the given value.
            /// </summary>
            /// <param name="value">The value to get alignment information for.</param>
            /// <returns>Pointer alignment in bytes (can be 1 byte).</returns>
            public UniformKind this[Value value] =>
                AnalysisResult.TryGetData(value, out var data)
                    ? data.Data.Kind
                    : UniformKind.Unknown;

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given value can be considered to be uniformly
            /// distributed across all threads in the current group. However, it
            /// pessimistically assumes that <see cref="UniformKind.Unknown"/> refers
            /// to a divergent value.
            /// </summary>
            /// <param name="value">The value to test.</param>
            /// <returns>True, if the given value can be considered uniform.</returns>
            public bool IsUniform(Value value) => this[value] == UniformKind.Uniform;

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new uniforms analysis.
        /// </summary>
        public static Uniforms Create(Method entryPoint) => new Uniforms();

        /// <summary>
        /// Applies a new alignment analysis to the given root method.
        /// </summary>
        /// <param name="entryPoint">The root (entry) method.</param>
        public static Info Apply(Method entryPoint)
        {
            var analysis = Create(entryPoint);
            var result = analysis.AnalyzeGlobalMethod(entryPoint, UniformKind.Uniform);
            return new Info(result);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new analysis implementation.
        /// </summary>
        protected Uniforms()
            : base(defaultValue: UniformKind.Unknown)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Returns initial and unconstrained information about whether the given value
        /// can be considered uniform.
        /// </summary>
        /// <param name="node">The IR node.</param>
        /// <returns>The uniform state of the given node.</returns>
        private static ValueInfo IsUniformByDefinition(Value node) =>
            node switch
            {
                // Thread-dependent values are divergent by definition
                LaneIdxValue _ => UniformKind.Divergent,
                GroupIndexValue _ => UniformKind.Divergent,
                // (Device-wide) constants are uniform by definition
                GridIndexValue _ => UniformKind.Uniform,
                ConstantNode _ => UniformKind.Uniform,
                UndefinedValue _ => UniformKind.Uniform,
                // Method calls can be considered uniform since each thread will perform
                // the same call (since we do not support virtual and jump-table based
                // calls at the moment)
                MethodCall _ => UniformKind.Uniform,
                // Unconditional branches can be considered uniform
                UnconditionalBranch _ => UniformKind.Uniform,
                // Conditional branches are considered to be unknown
                ConditionalBranch _ => UniformKind.Unknown,
                // All return terminators must be assumed to be divergent
                ReturnTerminator _ => UniformKind.Divergent,
                // All remaining values have an unknown state
                _ => UniformKind.Unknown
            };

        /// <summary>
        /// Creates initial analysis data using <see cref="IsUniformByDefinition"/>.
        /// </summary>
        protected override AnalysisValue<ValueInfo> CreateData(Value node) =>
            CreateValue(IsUniformByDefinition(node), node.Type);

        /// <summary>
        /// Returns the maximum of the first and the second kind.
        /// </summary>
        protected override ValueInfo Merge(ValueInfo first, ValueInfo second) =>
            (UniformKind)Math.Max((int)first.Kind, (int)second.Kind);

        /// <summary>
        /// Returns no type-based information.
        /// </summary>
        protected override AnalysisValue<ValueInfo>? TryProvide(TypeNode typeNode) =>
            null;

        /// <summary>
        /// Merges information about terminators.
        /// </summary>
        protected override AnalysisValue<ValueInfo>? TryMerge<TContext>(
            Value value,
            TContext context) =>
            value switch
            {
                // Conditional branches depend on their condition value
                ConditionalBranch condBranch => context[condBranch.Condition],
                // Use the default merge logic for all remaining values
                _ => null
            };

        #endregion
    }
}
