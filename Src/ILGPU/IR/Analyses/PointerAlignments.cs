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
using ILGPU.Util;
using System;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to determine safe alignment information for all pointer values.
    /// </summary>
    public class PointerAlignments : GlobalFixPointAnalysis<int, Forwards>
    {
        #region Nested Types

        /// <summary>
        /// Stores alignment information of an alignment analysis run.
        /// </summary>
        public readonly struct AlignmentInfo
        {
            #region Static

            /// <summary>
            /// Empty allocation information.
            /// </summary>
            public static readonly AlignmentInfo Empty =
                new AlignmentInfo(GlobalAnalysisValueResult<int>.Empty);

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new alignment analysis.
            /// </summary>
            internal AlignmentInfo(GlobalAnalysisValueResult<int> analysisResult)
            {
                AnalysisResult = analysisResult;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Stores a method value-alignment mapping.
            /// </summary>
            public GlobalAnalysisValueResult<int> AnalysisResult { get; }

            /// <summary>
            /// Returns pointer alignment information for the given value.
            /// </summary>
            /// <param name="value">The value to get alignment information for.</param>
            /// <returns>Pointer alignment in bytes (can be 1 byte).</returns>
            public readonly int this[Value value] =>
                AnalysisResult.TryGetData(value, out var data)
                    ? data.Data
                    : 1;

            /// <summary>
            /// Returns true if this alignment information object is empty.
            /// </summary>
            public readonly bool IsEmpty => AnalysisResult.IsEmpty;

            #endregion

            #region Methods

            /// <summary>
            /// Returns the alignment information determined and used for the given
            /// alloca.
            /// </summary>
            /// <param name="alloca">
            /// The alloca to get the alignment information for.
            /// </param>
            /// <returns>The determined and used alignment in bytes.</returns>
            public readonly int GetAllocaAlignment(Alloca alloca) =>
                GetAlignment(
                    alloca,
                    AllocaAlignments.GetInitialAlignment(alloca));

            /// <summary>
            /// Returns safe alignment information.
            /// </summary>
            /// <param name="value">
            /// The value for which to compute the alignment for.
            /// </param>
            /// <param name="safeMinAlignment">
            /// The safe minimum alignment in bytes.
            /// </param>
            /// <returns>The computed alignment.</returns>
            public readonly int GetAlignment(Value value, int safeMinAlignment) =>
                Math.Max(this[value], safeMinAlignment);

            /// <summary>
            /// Returns safe alignment information.
            /// </summary>
            /// <param name="value">
            /// The value for which to compute the alignment for.
            /// </param>
            /// <param name="safeMinTypeAlignment">
            /// The safe minimum type alignment.
            /// </param>
            /// <returns>The computed alignment.</returns>
            public readonly int GetAlignment(
                Value value,
                TypeNode safeMinTypeAlignment) =>
                GetAlignment(value, safeMinTypeAlignment.Alignment);

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new alignment analysis.
        /// </summary>
        /// <param name="globalAlignment">
        /// The initial alignment information of all pointers and views of the root
        /// method.
        /// </param>
        public static PointerAlignments Create(int globalAlignment) =>
            new PointerAlignments(globalAlignment);

        /// <summary>
        /// Applies a new alignment analysis to the given root method.
        /// </summary>
        /// <param name="rootMethod">The root (entry) method.</param>
        /// <param name="globalAlignment">
        /// The initial alignment information of all pointers and views of the root
        /// method.
        /// </param>
        public static AlignmentInfo Apply(Method rootMethod, int globalAlignment)
        {
            var analysis = Create(globalAlignment);
            var result = analysis.AnalyzeGlobalMethod(rootMethod, globalAlignment);
            return new AlignmentInfo(result);
        }

        #endregion

        #region Instance

        private readonly AllocaAlignments allocaAlignments = AllocaAlignments.Create();

        /// <summary>
        /// Constructs a new analysis implementation.
        /// </summary>
        /// <param name="globalAlignment">The global alignment information.</param>
        protected PointerAlignments(int globalAlignment)
            : base(defaultValue: 1)
        {
            GlobalAlignment = globalAlignment;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the global alignment in bytes.
        /// </summary>
        public int GlobalAlignment { get; }

        #endregion

        #region Methods

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
                case UndefinedValue _:
                    return int.MaxValue;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Creates initial analysis data.
        /// </summary>
        protected override AnalysisValue<int> CreateData(Value node) =>
            CreateValue(
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
        /// Creates alignment information for global pointer and view types.
        /// </summary>
        protected override AnalysisValue<int>? TryProvide(TypeNode typeNode) =>
            typeNode is AddressSpaceType
                ? CreateValue(GlobalAlignment, typeNode)
                : default(AnalysisValue<int>?);

        #endregion

        #region Merging

        /// <summary>
        /// Computes merged alignment information of the given
        /// <see cref="LoadFieldAddress"/> node.
        /// </summary>
        private static AnalysisValue<int> MergeLoadFieldAddress<TContext>(
            LoadFieldAddress lfa,
            TContext context)
            where TContext : IAnalysisValueContext<int>
        {
            // Determine the base alignment of the input address
            int baseAlignment = context[lfa.Source].Data;

            // Determine the alignment of the referenced field
            int fieldAlignment = lfa.StructureType[lfa.FieldSpan.Access].Alignment;

            // Use the minimum alignment information of both addresses. Note that this
            // is required to check for non-properly aligned fields.
            return CreateValue(
                Math.Min(baseAlignment, fieldAlignment),
                lfa.Type);
        }

        /// <summary>
        /// Computes merged alignment information of the given
        /// <see cref="LoadElementAddress"/> node.
        /// </summary>
        private static AnalysisValue<int> MergeLoadElementAddress<TContext>(
            LoadElementAddress lea,
            TContext context)
            where TContext : IAnalysisValueContext<int>
        {
            // Determine the base alignment of the input address
            int baseAlignment = context[lea.Source].Data;

            // Determine the alignment of the referenced element type (used for indexing)
            var elementType = (lea.Type as AddressSpaceType).ElementType;
            int typeAlignment = AllocaAlignments.GetAllocaTypeAlignment(elementType);

            // Check whether we have found a power of 2 != 0
            if (lea.Offset.Resolve() is PrimitiveValue primitiveValue &&
                primitiveValue.IsInt &&
                primitiveValue.RawValue > 1L &&
                Utilities.IsPowerOf2(primitiveValue.RawValue) &&
                primitiveValue.RawValue < int.MaxValue / typeAlignment)
            {
                // We can use a multiple of the type alignment
                typeAlignment *= (int)primitiveValue.RawValue;
            }

            // Use the minimum alignment information of both addresses. Note that this
            // is required to check for non-properly aligned accesses.
            return CreateValue(
                Math.Min(baseAlignment, typeAlignment),
                lea.Type);
        }

        /// <summary>
        /// Returns merged information about <see cref="LoadFieldAddress"/> and
        /// <see cref="LoadElementAddress"/> IR nodes.
        /// </summary>
        protected override AnalysisValue<int>? TryMerge<TContext>(
            Value value,
            TContext context) =>
            value switch
            {
                LoadFieldAddress lfa => MergeLoadFieldAddress(lfa, context),
                LoadElementAddress lea => MergeLoadElementAddress(lea, context),
                _ => null,
            };

        #endregion
    }
}
