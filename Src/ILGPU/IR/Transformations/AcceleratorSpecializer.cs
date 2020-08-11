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
    public class AcceleratorSpecializer : UnorderedTransformation
    {
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
            AcceleratorSpecializer specializer,
            AcceleratorTypeValue value) =>
            Specialize(context, value, (int)specializer.AcceleratorType);

        /// <summary>
        /// Specializes warp size values.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            AcceleratorSpecializer specializer,
            WarpSizeValue value)
        {
            if (!specializer.WarpSize.HasValue)
                return;
            Specialize(context, value, specializer.WarpSize.Value);
        }

        /// <summary>
        /// Specializes native pointer casts.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            AcceleratorSpecializer specializer,
            IntAsPointerCast value)
        {
            var convert = context.Builder.CreateConvert(
                value.Location,
                value.Value,
                specializer.IntPointerType);

            context.ReplaceAndRemove(value, convert);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<AcceleratorSpecializer> Rewriter =
            new Rewriter<AcceleratorSpecializer>();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static AcceleratorSpecializer()
        {
            Rewriter.Add<AcceleratorTypeValue>(Specialize);
            Rewriter.Add<WarpSizeValue>(Specialize);
            Rewriter.Add<IntAsPointerCast>(Specialize);
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
        /// Returns the target platform.
        /// </summary>
        public PrimitiveType IntPointerType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Applies an accelerator-specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            Rewriter.Rewrite(builder.SourceBlocks, builder, this);

        #endregion
    }
}

