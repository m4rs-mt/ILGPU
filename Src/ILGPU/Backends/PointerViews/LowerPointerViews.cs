// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LowerPointerViews.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Lowers view instances into pointer view implementations.
    /// </summary>
    public sealed class LowerPointerViews : LowerViews
    {
        /// <summary>
        /// Converts view types into pointer-based structure types.
        /// </summary>
        private sealed class PointerViewLowering : ViewTypeLowering
        {
            public PointerViewLowering(Method.Builder builder)
                : base(builder)
            { }

            /// <summary cref="TypeConverter{TType}.GetNumFields(TType)"/>
            protected override int GetNumFields(ViewType type) => 2;

            /// <summary cref="TypeConverter{TType}.ConvertType{TTypeContext}(TTypeContext, TType)"/>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ViewType type)
            {
                var pointerType = typeContext.CreatePointerType(
                    type.ElementType,
                    type.AddressSpace);
                var lengthType = typeContext.GetPrimitiveType(BasicValueType.Int32);

                return typeContext.CreateStructureType(
                    ImmutableArray.Create<TypeNode>(pointerType, lengthType));
            }

        }
        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            NewView value)
        {
            var viewInstance = context.Builder.CreateStructure(
                ImmutableArray.Create(
                    value.Pointer,
                    value.Length));
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
            var pointer = builder.CreateGetField(value.Source, new FieldSpan(0));
            var newPointer = builder.CreateLoadElementAddress(pointer, value.Offset);

            var subView = builder.CreateStructure(
                ImmutableArray.Create(newPointer, value.Length));
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
            var pointer = builder.CreateGetField(value.Value, new FieldSpan(0));
            var length = builder.CreateGetField(value.Value, new FieldSpan(1));

            var newPointer = builder.CreateAddressSpaceCast(pointer, value.TargetAddressSpace);
            var newInstance = builder.CreateStructure(
                ImmutableArray.Create(newPointer, length));
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
            var pointer = builder.CreateGetField(value.Value, new FieldSpan(0));
            var length = builder.CreateGetField(value.Value, new FieldSpan(1));

            // New pointer
            var newPointer = builder.CreatePointerCast(pointer, value.TargetElementType);

            // Compute new length: newLength = length * sourceElementSize / targetElementSize;
            var sourceElementType = (typeLowering[value] as ViewType).ElementType;
            var sourceElementSize = builder.CreateSizeOf(sourceElementType);
            var targetElementSize = builder.CreateSizeOf(value.TargetElementType);
            var newLength = builder.CreateArithmetic(
                builder.CreateArithmetic(length, sourceElementSize, BinaryArithmeticKind.Mul),
                targetElementSize, BinaryArithmeticKind.Div);

            var newInstance = builder.CreateStructure(
                ImmutableArray.Create(newPointer, newLength));
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
            var pointer = builder.CreateGetField(value.Source, new FieldSpan(0));
            var newLea = builder.CreateLoadElementAddress(pointer, value.ElementIndex);
            context.ReplaceAndRemove(value, newLea);
        }

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

        /// <summary cref="LowerTypes{TType}.CreateLoweringConverter(Method.Builder, Scope)"/>
        protected override TypeLowering<ViewType> CreateLoweringConverter(
            Method.Builder builder,
            Scope _) =>
            new PointerViewLowering(builder);

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);
    }
}
