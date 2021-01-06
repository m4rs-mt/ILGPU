// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;

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
                IRContext context)
            {
                Specializer = specializer;
                Context = context;
            }

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
        /// Specializes IO output operations via the instance method
        /// <see cref="Specialize(in RewriterContext, IRContext, WriteToOutput)"/> of
        /// the parent <paramref name="data"/> instance.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            SpecializerData data,
            WriteToOutput value) =>
            data.Specializer.Specialize(context, data.Context, value);

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
        public AcceleratorSpecializer(
            AcceleratorType acceleratorType,
            int? warpSize,
            PrimitiveType intPointerType)
        {
            AcceleratorType = acceleratorType;
            WarpSize = warpSize;
            IntPointerType = intPointerType;
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

        #endregion

        #region Methods

        /// <summary>
        /// Applies an accelerator-specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(
            IRContext context,
            Method.Builder builder) =>
            Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                new SpecializerData(this, context));

        /// <summary>
        /// Specializes IO output operations (if any). Note that this default
        /// implementation removes the output operations from the current program.
        /// </summary>
        /// <param name="context">The current rewriter context.</param>
        /// <param name="irContext">The parent IR context.</param>
        /// <param name="writeToOutput">The IO output operation.</param>
        protected virtual void Specialize(
            in RewriterContext context,
            IRContext irContext,
            WriteToOutput writeToOutput) =>
            context.Remove(writeToOutput);

        #endregion
    }
}

