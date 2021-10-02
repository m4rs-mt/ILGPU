// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerThreadIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Lowers internal high-level thread intrinsics.
    /// </summary>
    public sealed class LowerThreadIntrinsics : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract value lowering.
        /// </summary>
        /// <typeparam name="TValue">The thread value type.</typeparam>
        public interface ILoweringImplementation<TValue>
            where TValue : ThreadValue
        {
            /// <summary>
            /// Lowers the given thread value.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <param name="source">The source value.</param>
            /// <param name="newVariable">The new variable.</param>
            /// <returns>The created value.</returns>
            ValueReference Lower(
                BasicBlock.Builder builder,
                TValue source,
                Value newVariable);
        }

        /// <summary>
        /// Lowers broadcast operations.
        /// </summary>
        public readonly struct BroadcastLowering :
            ILoweringImplementation<Broadcast>
        {
            /// <summary>
            /// Lowers a broadcast value by constructing a new one.
            /// </summary>
            public ValueReference Lower(
                BasicBlock.Builder builder,
                Broadcast source,
                Value newVariable) =>
                builder.CreateBroadcast(
                    source.Location,
                    newVariable,
                    source.Origin,
                    source.Kind);
        }

        /// <summary>
        /// Lowers warp shuffle operations.
        /// </summary>
        public readonly struct WarpShuffleLowering :
            ILoweringImplementation<WarpShuffle>
        {
            /// <summary>
            /// Lowers a warp shuffle value by constructing a new one.
            /// </summary>
            public ValueReference Lower(
                BasicBlock.Builder builder,
                WarpShuffle source,
                Value newVariable) =>
                builder.CreateShuffle(
                    source.Location,
                    newVariable,
                    source.Origin,
                    source.Kind);
        }

        /// <summary>
        /// Lowers sub warp shuffle operations.
        /// </summary>
        public readonly struct SubWarpShuffleLowering :
            ILoweringImplementation<SubWarpShuffle>
        {
            /// <summary>
            /// Lowers a sub warp shuffle value by constructing a new one.
            /// </summary>
            public ValueReference Lower(
                BasicBlock.Builder builder,
                SubWarpShuffle source,
                Value newVariable) =>
                builder.CreateShuffle(
                    source.Location,
                    newVariable,
                    source.Origin,
                    source.Width,
                    source.Kind);
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Lowers a primitive type.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <typeparam name="TLoweringImplementation">
        /// The implementation type.
        /// </typeparam>
        /// <param name="context">The current rewriter context.</param>
        /// <param name="sourceValue">The source value to get the values from.</param>
        /// <param name="variable">The source variable.</param>
        /// <returns>The lowered thread value.</returns>
        private static Value LowerPrimitive<TValue, TLoweringImplementation>(
            RewriterContext context,
            TValue sourceValue,
            Value variable)
            where TValue : ThreadValue
            where TLoweringImplementation : struct, ILoweringImplementation<TValue>
        {
            var builder = context.Builder;
            var primitiveType = variable.Type as PrimitiveType;
            Value value = variable;
            if (primitiveType.BasicValueType < BasicValueType.Int32)
            {
                value = builder.CreateConvert(
                    sourceValue.Location,
                    value,
                    builder.GetPrimitiveType(BasicValueType.Int32));
            }

            TLoweringImplementation loweringImplementation = default;
            var result = loweringImplementation.Lower(
                builder,
                sourceValue,
                value);
            if (primitiveType.BasicValueType < BasicValueType.Int32)
            {
                result = builder.CreateConvert(
                    sourceValue.Location,
                    result,
                    variable.Type);
            }
            return result;
        }

        /// <summary>
        /// Lowers a type.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <typeparam name="TLoweringImplementation">
        /// The implementation type.
        /// </typeparam>
        /// <param name="context">The current rewriter context.</param>
        /// <param name="value">The source value to get the values from.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Lower<TValue, TLoweringImplementation>(
            in RewriterContext context,
            TValue value)
            where TValue : ThreadValue
            where TLoweringImplementation : struct, ILoweringImplementation<TValue>
        {
            var newValue = context.LowerValue(
                value,
                value.Variable,
                LowerPrimitive<TValue, TLoweringImplementation>);
            context.ReplaceAndRemove(value, newValue);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter Rewriter = new Rewriter();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static LowerThreadIntrinsics()
        {
            Rewriter.Add<Broadcast>(
                broadcast => !broadcast.IsBuiltIn,
                (context, value) =>
                    Lower<Broadcast, BroadcastLowering>(context, value));
            Rewriter.Add<WarpShuffle>(
                shuffle => !shuffle.IsBuiltIn,
                (context, value) =>
                    Lower<WarpShuffle, WarpShuffleLowering>(context, value));
            Rewriter.Add<SubWarpShuffle>(
                shuffle => !shuffle.IsBuiltIn,
                (context, value) =>
                    Lower<SubWarpShuffle, SubWarpShuffleLowering>(context, value));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the lowering of thread intrinsics transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            Rewriter.Rewrite(builder.SourceBlocks, builder);

        #endregion
    }
}
