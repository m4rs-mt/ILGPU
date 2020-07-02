// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PointerAlignments.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to determine safe alignment information for all pointer values.
    /// </summary>
    public sealed class PointerAlignments
    {
        #region Nested Types

        /// <summary>
        /// Stores alignment information of a specific method.
        /// </summary>
        public readonly struct AlignmentInfo
        {
            internal AlignmentInfo(
                Method method,
                Dictionary<Value, int> alignments)
            {
                Method = method;
                Alignments = alignments;
            }

            /// <summary>
            /// Returns the underlying alignments object.
            /// </summary>
            private Dictionary<Value, int> Alignments { get; }

            /// <summary>
            /// Returns the associated method.
            /// </summary>
            public Method Method { get; }

            /// <summary>
            /// Returns pointer alignment information for the given value.
            /// </summary>
            /// <param name="value">The value to get alignment information for.</param>
            /// <returns>Pointer alignment in bytes (can be 1 byte).</returns>
            public readonly int this[Value value] =>
                Alignments.TryGetValue(value, out int alignment)
                ? alignment
                : 1;
        }

        /// <summary>
        /// The actual value merger to compute alignment information.
        /// </summary>
        private readonly struct Merger : IAnalysisValueMerger<int>
        {
            /// <summary>
            /// Returns the minimum of the first and the second value.
            /// </summary>
            public readonly int Merge(int first, int second) =>
                Math.Min(first, second);

            /// <summary>
            /// Returns merged information about <see cref="LoadFieldAddress"/> and
            /// <see cref="LoadElementAddress"/> IR nodes.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly AnalysisValue<int>? TryMerge<TContext>(
                Value value,
                TContext context)
                where TContext : IAnalysisValueContext<int> =>
                value switch
                {
                    LoadFieldAddress lfa =>
                        AnalysisValue.Create(
                            Math.Min(
                                context[lfa.Source].Data,
                                lfa.StructureType[lfa.FieldSpan.Access].Alignment),
                                lfa.Type),
                    LoadElementAddress lea =>
                        AnalysisValue.Create(
                            Math.Max(
                                context[lea.Source].Data,
                                (lea.Type as IAddressSpaceType).ElementType.Alignment),
                            lea.Type),
                    _ => null,
                };
        }

        /// <summary>
        /// Provides initial alignment information about pointer arguments.
        /// </summary>
        private readonly struct Provider : IAnalysisValueProvider<int>
        {
            /// <summary>
            /// Constructs a new provider with the given alignment.
            /// </summary>
            /// <param name="globalAlignment">The global alignment information.</param>
            public Provider(int globalAlignment)
            {
                GlobalAlignment = globalAlignment;
            }

            /// <summary>
            /// Returns the global alignment of pointers in memory.
            /// </summary>
            public int GlobalAlignment { get; }

            /// <summary>
            /// Returns 1.
            /// </summary>
            public readonly int DefaultValue => 1;

            /// <summary>
            /// Creates alignment information for global pointer and view types.
            /// </summary>
            public readonly AnalysisValue<int>? TryProvide(TypeNode typeNode) =>
                typeNode is IAddressSpaceType
                ? AnalysisValue.Create(GlobalAlignment, typeNode)
                : (AnalysisValue<int>?)null;
        }

        /// <summary>
        /// The actual alignment information implementation.
        /// </summary>
        private readonly struct AnalysisImplementation :
            IGlobalFixPointAnalysis<
                Dictionary<Value, int>,
                AnalysisValue<int>,
                Forwards>
        {
            /// <summary>
            /// Returns initial and unconstrained alignment information.
            /// </summary>
            /// <param name="node">The IR node.</param>
            /// <returns>The initial alignment information.</returns>
            private static int GetInitialAlignment(Value node)
            {
                switch (node)
                {
                    case Alloca alloca:
                        return alloca.AllocaType.Alignment;
                    case NewView _:
                    case BaseAddressSpaceCast _:
                    case SubViewValue _:
                    case LoadElementAddress _:
                    case LoadFieldAddress _:
                    case GetField _:
                    case SetField _:
                    case StructureValue _:
                    case Load _:
                    case Store _:
                    case PhiValue _:
                    case PrimitiveValue _:
                    case NullValue _:
                        return int.MaxValue;
                    default:
                        return 1;
                }
            }

            /// <summary>
            /// Creates initial analysis data.
            /// </summary>
            public AnalysisValue<int> CreateData(Value node) =>
                AnalysisValue.Create(GetInitialAlignment(node), node.Type);

            /// <summary>
            /// Creates a new method value dictionary.
            /// </summary>
            public Dictionary<Value, int> CreateMethodData(Method _) =>
                new Dictionary<Value, int>();

            /// <summary>
            /// Merges updated value information.
            /// </summary>
            bool IFixPointAnalysis<AnalysisValue<int>, Value, Forwards>.
                Update<TContext>(
                Value value,
                TContext context) =>
                AnalysisValue.MergeTo<int, Merger, TContext>(
                    value,
                    new Merger(),
                    context);

            /// <summary>
            /// Registers all values in the internal method mapping.
            /// </summary>
            void IGlobalFixPointAnalysis<
                Dictionary<Value, int>,
                AnalysisValue<int>,
                Forwards>.UpdateMethod<TContext>(
                Method method,
                ImmutableArray<AnalysisValue<int>> arguments,
                Dictionary<Value, AnalysisValue<int>> valueMapping,
                TContext context)
            {
                var data = context[method];
                foreach (var entry in valueMapping)
                    data.Merge(entry.Key, entry.Value.Data, new Merger());
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// An empty value mapping.
        /// </summary>
        private readonly static Dictionary<Value, int> EmptyMapping =
            new Dictionary<Value, int>();

        /// <summary>
        /// Represents no pointer alignment information.
        /// </summary>
        public static readonly PointerAlignments Empty = new PointerAlignments();

        /// <summary>
        /// Creates a new alignment analysis.
        /// </summary>
        /// <param name="rootMethod">The root (entry) method.</param>
        /// <param name="globalAlignment">
        /// The initial alignment information of all pointers and views of the entry
        /// method.
        /// </param>
        /// <returns>The created alignment analysis.</returns>
        public static PointerAlignments Create(
            Method rootMethod,
            int globalAlignment)
        {
            var parameters = ImmutableArray.CreateBuilder<AnalysisValue<int>>(
                rootMethod.NumParameters);

            foreach (var param in rootMethod.Parameters)
            {
                parameters.Add(AnalysisValue.Create<int, Merger, Provider>(
                    param.Type,
                    new Merger(),
                    new Provider(globalAlignment)));
            }

            return new PointerAlignments(
                rootMethod,
                parameters.MoveToImmutable());
        }

        #endregion

        #region Instance

        /// <summary>
        /// Stores a method value-alignment mapping.
        /// </summary>
        private readonly Dictionary<Method, Dictionary<Value, int>> alignments;

        /// <summary>
        /// Constructs an empty pointer alignment analysis.
        /// </summary>
        private PointerAlignments()
        {
            alignments = new Dictionary<Method, Dictionary<Value, int>>();
        }

        /// <summary>
        /// Constructs a new pointer alignment analysis.
        /// </summary>
        /// <param name="rootMethod">The root (entry) method.</param>
        /// <param name="parameterValues">The initial alignment information.</param>
        private PointerAlignments(
            Method rootMethod,
            ImmutableArray<AnalysisValue<int>> parameterValues)
        {
            var impl = new AnalysisImplementation();
            alignments = impl.AnalyzeGlobal(rootMethod, parameterValues);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns pointer alignment information for the given method.
        /// </summary>
        /// <param name="method">The method to get alignment information for.</param>
        /// <returns>Resolved pointer alignment information.</returns>
        public AlignmentInfo this[Method method] =>
            alignments.TryGetValue(method, out var result)
            ? new AlignmentInfo(method, result)
            : new AlignmentInfo(method, EmptyMapping);

        /// <summary>
        /// Returns pointer alignment information for the given value.
        /// </summary>
        /// <param name="value">The value to get alignment information for.</param>
        /// <returns>Pointer alignment in bytes (can be 1 byte).</returns>
        public int this[Value value] => this[value.Method][value];

        #endregion

        #region Methods

        /// <summary>
        /// Returns safe alignment information.
        /// </summary>
        /// <param name="value">The value for which to compute the alignment for.</param>
        /// <param name="safeMinAlignment">The safe minimum alignment in bytes.</param>
        /// <returns>The computed alignment.</returns>
        public int GetAlignment(Value value, int safeMinAlignment) =>
            Math.Max(this[value], safeMinAlignment);

        /// <summary>
        /// Returns safe alignment information.
        /// </summary>
        /// <param name="value">The value for which to compute the alignment for.</param>
        /// <param name="safeMinTypeAlignment">The safe minimum type alignment.</param>
        /// <returns>The computed alignment.</returns>
        public int GetAlignment(Value value, TypeNode safeMinTypeAlignment) =>
            GetAlignment(value, safeMinTypeAlignment.Alignment);

        #endregion
    }
}
