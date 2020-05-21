// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LowerPointerViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Lowers view instances into pointer view implementations.
    /// </summary>
    public sealed class LowerPointerViews : LowerViews
    {
        #region Type Lowering

        /// <summary>
        /// Converts view types into pointer-based structure types.
        /// </summary>
        private sealed class PointerViewLowering : ViewTypeLowering
        {
            public PointerViewLowering(Method.Builder builder)
                : base(builder)
            { }

            /// <summary>
            /// Returns the number of fields per view type.
            /// </summary>
            protected override int GetNumFields(ViewType type) => 2;

            /// <summary>
            /// Converts the given view type into a structure with two elements.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ViewType type)
            {
                var builder = typeContext.CreateStructureType(2);
                builder.Add(typeContext.CreatePointerType(
                    type.ElementType,
                    type.AddressSpace));
                builder.Add(typeContext.GetPrimitiveType(BasicValueType.Int32));
                return builder.Seal();
            }
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            NewView value)
        {
            var viewInstance = context.Builder.CreateDynamicStructure(
                value.Location,
                value.Pointer,
                value.Length);
            context.ReplaceAndRemove(value, viewInstance);
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            GetViewLength value)
        {
            var length = context.Builder.CreateGetField(
                value.Location,
                value.View,
                new FieldSpan(1));
            context.ReplaceAndRemove(value, length);
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            SubViewValue value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Source,
                new FieldSpan(0));
            var newPointer = builder.CreateLoadElementAddress(
                location,
                pointer,
                value.Offset);

            var subView = builder.CreateDynamicStructure(
                location,
                newPointer,
                value.Length);
            context.ReplaceAndRemove(value, subView);
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            AddressSpaceCast value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(0));
            var length = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(1));

            var newPointer = builder.CreateAddressSpaceCast(
                location,
                pointer,
                value.TargetAddressSpace);
            var newInstance = builder.CreateDynamicStructure(
                location,
                newPointer,
                length);
            context.ReplaceAndRemove(value, newInstance);
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> typeLowering,
            ViewCast value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(0));
            var length = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(1));

            // New pointer
            var newPointer = builder.CreatePointerCast(
                location,
                pointer,
                value.TargetElementType);

            // Compute new length:
            // newLength = length * sourceElementSize / targetElementSize;
            var sourceElementType = (typeLowering[value] as ViewType).ElementType;
            var sourceElementSize = builder.CreateSizeOf(
                location,
                sourceElementType);
            var targetElementSize = builder.CreateSizeOf(
                location,
                value.TargetElementType);
            var newLength = builder.CreateArithmetic(
                location,
                builder.CreateArithmetic(
                    location,
                    length,
                    sourceElementSize,
                    BinaryArithmeticKind.Mul),
                targetElementSize, BinaryArithmeticKind.Div);

            var newInstance = builder.CreateDynamicStructure(
                location,
                newPointer,
                newLength);
            context.ReplaceAndRemove(value, newInstance);
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            LoadElementAddress value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Source,
                new FieldSpan(0));
            var newLea = builder.CreateLoadElementAddress(
                location,
                pointer,
                value.ElementIndex);
            context.ReplaceAndRemove(value, newLea);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<TypeLowering<ViewType>> Rewriter =
            new Rewriter<TypeLowering<ViewType>>();

        /// <summary>
        /// Initializes all rewriter patterns.
        /// </summary>
        static LowerPointerViews()
        {
            AddRewriters(
                Rewriter,
                Lower,
                Lower,
                Lower,
                Lower,
                Lower,
                Lower);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new pointer view lowering transformation.
        /// </summary>
        public LowerPointerViews() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="PointerViewLowering"/> converter.
        /// </summary>
        protected override TypeLowering<ViewType> CreateLoweringConverter(
            Method.Builder builder) =>
            new PointerViewLowering(builder);

        /// <summary>
        /// Applies the pointer view lowering transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);

        #endregion
    }
}
