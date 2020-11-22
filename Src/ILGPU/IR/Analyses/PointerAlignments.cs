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
                AnalysisValueMapping<int> alignments)
            {
                Method = method;
                Alignments = alignments;
            }

            /// <summary>
            /// Returns the underlying alignments object.
            /// </summary>
            private AnalysisValueMapping<int> Alignments { get; }

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
                Alignments.TryGetValue(value, out var alignment)
                ? alignment.Data
                : 1;
        }

        /// <summary>
        /// Models the internal pointer alignment analysis.
        /// </summary>
        private sealed class AnalysisImplementation :
            GlobalFixPointAnalysis<int, Forwards>
        {
            private readonly AllocaAlignments allocaAlignments =
                AllocaAlignments.Create();

            /// <summary>
            /// Constructs a new analysis implementation.
            /// </summary>
            /// <param name="globalAlignment">The global alignment information.</param>
            public AnalysisImplementation(int globalAlignment)
                : base(
                      defaultValue: 1,
                      initialValue: globalAlignment)
            { }

            /// <summary>
            /// Creates initial analysis data.
            /// </summary>
            protected override AnalysisValue<int> CreateData(Value node) =>
                Create(
                    node is Alloca alloca
                        ? allocaAlignments.ComputeAllocaAlignment(alloca)
                        : GetInitialAlignment(node),
                    node.Type);

            /// <summary>
            /// Returns the minimum of the first and the second value.
            /// </summary>
            protected override int Merge(int first, int second) =>
                Math.Min(first, second);

            /// <summary>
            /// Returns merged information about <see cref="LoadFieldAddress"/> and
            /// <see cref="LoadElementAddress"/> IR nodes.
            /// </summary>
            protected override AnalysisValue<int>? TryMerge<TContext>(
                Value value,
                TContext context) =>
                value switch
                {
                    LoadFieldAddress lfa =>
                        Create(
                            Math.Min(
                                context[lfa.Source].Data,
                                lfa.StructureType[lfa.FieldSpan.Access].Alignment),
                                lfa.Type),
                    LoadElementAddress lea =>
                        Create(
                            Math.Max(
                                context[lea.Source].Data,
                                (lea.Type as IAddressSpaceType).ElementType.Alignment),
                            lea.Type),
                    _ => null,
                };

            /// <summary>
            /// Creates alignment information for global pointer and view types.
            /// </summary>
            protected override AnalysisValue<int>? TryProvide(TypeNode typeNode) =>
                typeNode is IAddressSpaceType
                ? Create(InitialValue, typeNode)
                : (AnalysisValue<int>?)null;
        }

        #endregion

        #region Static

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
                    return AllocaAlignments.GetInitialAlignment(alloca);
                case AlignViewTo alignTo:
                    // Use a compile-time known alignment constant for the alignment
                    // information instead of type-based alignment reasoning
                    return alignTo.GetAlignmentConstant();
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
        /// An empty value mapping.
        /// </summary>
        private readonly static AnalysisValueMapping<int> EmptyMapping =
            AnalysisValueMapping.Create<int>();

        /// <summary>
        /// Represents no pointer alignment information.
        /// </summary>
        public static readonly PointerAlignments Empty = new PointerAlignments();

        /// <summary>
        /// Creates a new alignment analysis.
        /// </summary>
        /// <param name="rootMethod">The root (entry) method.</param>
        /// <param name="globalAlignment">
        /// The initial alignment information of all pointers and views of the root
        /// method.
        /// </param>
        public static PointerAlignments Create(Method rootMethod, int globalAlignment) =>
            new PointerAlignments(rootMethod, globalAlignment);

        #endregion

        #region Instance

        /// <summary>
        /// Stores a method value-alignment mapping.
        /// </summary>
        private readonly Dictionary<Method, AnalysisValueMapping<int>> alignments;

        /// <summary>
        /// Constructs an empty pointer alignment analysis.
        /// </summary>
        private PointerAlignments()
        {
            alignments = new Dictionary<Method, AnalysisValueMapping<int>>();
        }

        /// <summary>
        /// Constructs a new alignment analysis.
        /// </summary>
        private PointerAlignments(Method rootMethod, int globalAlignment)
        {
            var impl = new AnalysisImplementation(globalAlignment);
            alignments = impl.AnalyzeGlobal(rootMethod);
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
        /// Returns the alignment information determined and used for the given alloca.
        /// </summary>
        /// <param name="alloca">The alloca to get the alignment information for.</param>
        /// <returns>The determined and used alignment in bytes.</returns>
        public int GetAllocaAlignment(Alloca alloca) => this[alloca];

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
