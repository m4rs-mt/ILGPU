// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Collections.Generic;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a device specializer that instantiates device-specific constants
    /// and updates device-specific functionality.
    /// </summary>
    /// <remarks>
    /// Note that this class does not perform recursive specialization operations.
    /// </remarks>
    public class AcceleratorSpecializer : SequentialUnorderedTransformation
    {
        #region Nested Types

        private readonly struct SpecializerData
        {
            /// <summary>
            /// Constructs a new data instance.
            /// </summary>
            public SpecializerData(
                AcceleratorSpecializer specializer,
                IRContext context,
                List<Value> toImplement)
            {
                ToImplement = toImplement;
                Specializer = specializer;
                Context = context;
            }

            /// <summary>
            /// A list of values to be implemented in the next step.
            /// </summary>
            public List<Value> ToImplement { get; }

            /// <summary>
            /// Returns the parent specializer instance.
            /// </summary>
            public AcceleratorSpecializer Specializer { get; }

            /// <summary>
            /// Returns the current IR context.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Returns the current accelerator type.
            /// </summary>
            public readonly AcceleratorType AcceleratorType =>
                Specializer.AcceleratorType;

            /// <summary>
            /// Returns the current warp size (if any).
            /// </summary>
            public readonly int? WarpSize => Specializer.WarpSize;

            /// <summary>
            /// Returns the target-platform specific integer pointer type.
            /// </summary>
            public readonly PrimitiveType IntPointerType => Specializer.IntPointerType;

            /// <summary>
            /// Returns true if assertions are enabled.
            /// </summary>
            public readonly bool EnableAssertions => Specializer.EnableAssertions;

            /// <summary>
            /// Returns true if IO is enabled.
            /// </summary>
            public readonly bool EnableIOOperations => Specializer.EnableIOOperations;
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Specializes accelerator-specific values.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            Value value,
            int constant)
        {
            var newValue = context.Builder.CreatePrimitiveValue(
                value.Location,
                constant);
            context.ReplaceAndRemove(value, newValue);
        }

        /// <summary>
        /// Specializes accelerator-type values.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            AcceleratorTypeValue value) =>
            Specialize(context, value, (int)data.AcceleratorType);

        /// <summary>
        /// Specializes warp size values.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            WarpSizeValue value)
        {
            var warpSizeValue = data.WarpSize;
            if (!warpSizeValue.HasValue)
                return;
            Specialize(context, value, warpSizeValue.Value);
        }

        /// <summary>
        /// Returns true if we have to adjust the source cast operation.
        /// </summary>
        private static bool CanSpecialize(
            SpecializerData data,
            IntAsPointerCast value) =>
            value.SourceType != data.IntPointerType;

        /// <summary>
        /// Specializes int to native pointer casts.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            IntAsPointerCast value)
        {
            // Convert from int -> native int type -> pointer
            var builder = context.Builder;

            // int -> native int type
            var convertToNativeInt = builder.CreateConvert(
                value.Location,
                value.Value,
                data.IntPointerType);

            // native int type -> pointer
            var convert = builder.CreateIntAsPointerCast(
                value.Location,
                convertToNativeInt);

            context.ReplaceAndRemove(value, convert);
        }

        /// <summary>
        /// Returns true if we have to adjust the source cast operation.
        /// </summary>
        private static bool CanSpecialize(
            SpecializerData data,
            PointerAsIntCast value) =>
            value.TargetType != data.IntPointerType;

        /// <summary>
        /// Specializes native pointer to int casts.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            PointerAsIntCast value)
        {
            // Convert from ptr -> native int type -> desired int type
            var builder = context.Builder;

            // ptr -> native int type
            var convertToNativeType = builder.CreatePointerAsIntCast(
                value.Location,
                value.Value,
                data.IntPointerType.BasicValueType);

            // native int type -> desired int type
            var convert = builder.CreateConvert(
                value.Location,
                convertToNativeType,
                value.TargetType);

            context.ReplaceAndRemove(value, convert);
        }

        /// <summary>
        /// Removes or collects debug operations.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            DebugAssertOperation value)
        {
            if (data.EnableAssertions)
                data.ToImplement.Add(value);
            else
                context.Remove(value);
        }

        /// <summary>
        /// Removes or collects IO operations.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            WriteToOutput value)
        {
            if (data.EnableIOOperations)
                data.ToImplement.Add(value);
            else
                context.Remove(value);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<SpecializerData> Rewriter =
            new Rewriter<SpecializerData>();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static AcceleratorSpecializer()
        {
            Rewriter.Add<AcceleratorTypeValue>(Specialize);
            Rewriter.Add<WarpSizeValue>(Specialize);

            Rewriter.Add<DebugAssertOperation>(Specialize);
            Rewriter.Add<WriteToOutput>(Specialize);

            Rewriter.Add<IntAsPointerCast>(CanSpecialize, Specialize);
            Rewriter.Add<PointerAsIntCast>(CanSpecialize, Specialize);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new device specializer.
        /// </summary>
        /// <param name="acceleratorType">The accelerator type.</param>
        /// <param name="warpSize">The warp size (if any).</param>
        /// <param name="intPointerType">The native integer pointer type.</param>
        /// <param name="enableAssertions">True, if the assertions are enabled.</param>
        /// <param name="enableIOOperations">True, if the IO is enabled.</param>
        public AcceleratorSpecializer(
            AcceleratorType acceleratorType,
            int? warpSize,
            PrimitiveType intPointerType,
            bool enableAssertions,
            bool enableIOOperations)
        {
            AcceleratorType = acceleratorType;
            WarpSize = warpSize;
            IntPointerType = intPointerType;
            EnableAssertions = enableAssertions;
            EnableIOOperations = enableIOOperations;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current accelerator type.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

        /// <summary>
        /// Returns the current warp size (if any).
        /// </summary>
        public int? WarpSize { get; }

        /// <summary>
        /// Returns the target-platform specific integer pointer type.
        /// </summary>
        public PrimitiveType IntPointerType { get; }

        /// <summary>
        /// Returns true if assertions are enabled.
        /// </summary>
        public bool EnableAssertions { get; }

        /// <summary>
        /// Returns true if debug output is enabled.
        /// </summary>
        public bool EnableIOOperations { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Applies an accelerator-specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(
            IRContext context,
            Method.Builder builder)
        {
            var toImplement = new List<Value>(16);
            var data = new SpecializerData(this, context, toImplement);
            if (!Rewriter.Rewrite(builder.SourceBlocks, builder, data))
                return false;

            foreach (var value in toImplement)
            {
                switch (value)
                {
                    case DebugAssertOperation assert:
                        Implement(context, builder, builder[assert.BasicBlock], assert);
                        break;
                    case WriteToOutput write:
                        Implement(context, builder, builder[write.BasicBlock], write);
                        break;
                    default:
                        throw builder.GetInvalidOperationException();
                }
            }
            return true;
        }

        /// <summary>
        /// Specializes debug output operations (if any). Note that this default
        /// implementation removes the output operations from the current program.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="methodBuilder">The parent method builder.</param>
        /// <param name="builder">The current block builder.</param>
        /// <param name="debugAssert">The debug assert operation.</param>
        protected virtual void Implement(
            IRContext context,
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            DebugAssertOperation debugAssert) =>
            builder.Remove(debugAssert);

        /// <summary>
        /// Specializes IO output operations (if any). Note that this default
        /// implementation removes the output operations from the current program.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="methodBuilder">The parent method builder.</param>
        /// <param name="builder">The current block builder.</param>
        /// <param name="writeToOutput">The IO output operation.</param>
        protected virtual void Implement(
            IRContext context,
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            WriteToOutput writeToOutput) =>
            builder.Remove(writeToOutput);

        #endregion
    }
}

