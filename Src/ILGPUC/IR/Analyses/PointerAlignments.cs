// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAlignments.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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

        /// <summary>
        /// Tries to determine power of 2 information for the given unary operation.
        /// </summary>
        /// <param name="unary">The unary operation to analyze.</param>
        /// <returns>The power of 2 value (if any).</returns>
        private static int? TryGetPowerOf2(UnaryArithmeticValue unary) =>
            unary.Kind switch
            {
                UnaryArithmeticKind.Abs => TryGetPowerOf2(unary.Value),
                _ => null
            };

        /// <summary>
        /// Tries to determine power of 2 information for the given binary operation.
        /// </summary>
        /// <param name="binary">The binary operation to analyze.</param>
        /// <returns>The power of 2 value (if any).</returns>
        private static int? TryGetPowerOf2(BinaryArithmeticValue binary) =>
            binary.Kind switch
            {
                // Check whether either the left or the right operand are a power of 2
                BinaryArithmeticKind.Mul =>
                    TryGetPowerOf2(binary.Left) ??
                    TryGetPowerOf2(binary.Right),
                // Check whether we can determine a power of 2 of the left operand or
                // whether the RHS of the SHL operation is a primitive value
                BinaryArithmeticKind.Shl =>
                    TryGetPowerOf2(binary.Left) ??
                    (binary.Right.Resolve() is PrimitiveValue shlPrimitive &&
                    shlPrimitive.Int32Value > 0
                    ? (int?)(shlPrimitive.Int32Value * 2)
                    : null),
                _ => null,
            };

        /// <summary>
        /// Tries to determine a power of 2 for the given value (if any could be
        /// determined).
        /// </summary>
        /// <param name="value">The value to analyze.</param>
        /// <returns>The power of 2 value (if any).</returns>
        private static int? TryGetPowerOf2(Value value)
        {
            // Ensure that the value is operating on an integer type
            value.Assert(value.BasicValueType.IsInt());
            return value switch
            {
                // Check whether the value is a power of two in the case of a raw value
                PrimitiveValue primitive =>
                    primitive.Int32Value > 1 &&
                    Utilities.IsPowerOf2(primitive.Int32Value)
                    ? (int?)primitive.Int32Value
                    : null,
                // Propagate information in the presence of a arithmetic operations
                UnaryArithmeticValue unary => TryGetPowerOf2(unary),
                BinaryArithmeticValue binary => TryGetPowerOf2(binary),
                _ => null
            };
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
                case BaseAlignOperationValue alignment:
                    // Use a compile-time known alignment constant for the alignment
                    // information instead of type-based alignment reasoning
                    return alignment.GetAlignmentConstant();
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
        /// <see cref="BaseAlignOperationValue"/> node.
        /// </summary>
        private static AnalysisValue<int> MergeAlignmentValue<TContext>(
            BaseAlignOperationValue align,
            TContext context)
            where TContext : IAnalysisValueContext<int>
        {
            // Determine the base alignment of the input address
            int baseAlignment = context[align.Source].Data;

            // Simply assume the specified alignment information
            int newAlignment = align.GetAlignmentConstant();

            // Take the maximum of both values to compensate cases in which the alignment
            // constant could not be properly resolved at compile time
            return CreateValue(
                Math.Max(baseAlignment, newAlignment),
                align.Type);
        }

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
            var elementType = lea.Type.AsNotNullCast<AddressSpaceType>().ElementType;
            int typeAlignment = AllocaAlignments.GetAllocaTypeAlignment(elementType);

            // Check whether we have found a power of 2 != 0
            int? powerOf2 = TryGetPowerOf2(lea.Offset);
            if (powerOf2.HasValue && powerOf2.Value > 0)
            {
                // We can use a multiple of the type alignment
                typeAlignment *= powerOf2.Value;
            }

            // Use the minimum alignment information of both addresses. Note that this
            // is required to check for non-properly aligned accesses.
            return CreateValue(
                Math.Min(baseAlignment, typeAlignment),
                lea.Type);
        }

        /// <summary>
        /// Returns merged information about <see cref="LoadFieldAddress"/>,
        /// <see cref="LoadElementAddress"/> and <see cref="BaseAlignOperationValue"/> IR
        /// nodes.
        /// </summary>
        protected override AnalysisValue<int>? TryMerge<TContext>(
            Value value,
            TContext context) =>
            value switch
            {
                BaseAlignOperationValue align => MergeAlignmentValue(align, context),
                LoadFieldAddress lfa => MergeLoadFieldAddress(lfa, context),
                LoadElementAddress lea => MergeLoadElementAddress(lea, context),
                _ => null,
            };

        #endregion
    }
}
