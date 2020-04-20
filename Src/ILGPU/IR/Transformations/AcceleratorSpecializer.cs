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
            var newValue = context.Builder.CreatePrimitiveValue(constant);
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
        /// Specializes size-of values.
        /// </summary>
        private static void Specialize(
            RewriterContext context,
            AcceleratorSpecializer specializer,
            SizeOfValue value) =>
            Specialize(
                context,
                value,
                value.TargetType.Size);

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
            Rewriter.Add<SizeOfValue>(Specialize);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new device specializer.
        /// </summary>
        /// <param name="acceleratorType">The accelerator type.</param>
        /// <param name="warpSize">The warp size (if any).</param>
        public AcceleratorSpecializer(AcceleratorType acceleratorType, int? warpSize)
        {
            AcceleratorType = acceleratorType;
            WarpSize = warpSize;
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

        #endregion

        #region Methods

        /// <summary>
        /// Applies an accelerator-specialization transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            Rewriter.Rewrite(builder, this);

        #endregion
    }
}

