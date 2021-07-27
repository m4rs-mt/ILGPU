// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LowerViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Lowers views (values and types) into platform specific instances.
    /// </summary>
    public abstract class LowerViews : LowerTypes<ViewType>
    {
        /// <summary>
        /// An abstract view type converter.
        /// </summary>
        protected abstract class ViewTypeLowering : TypeLowering<ViewType>
        {
            /// <summary>
            /// Constructs a new type lowering without a parent type context.
            /// </summary>
            protected ViewTypeLowering() { }

            /// <summary>
            /// Constructs a new type lowering.
            /// </summary>
            /// <param name="builder">The parent builder.</param>
            protected ViewTypeLowering(Method.Builder builder)
                : this(builder.TypeContext)
            { }

            /// <summary>
            /// Constructs a new type lowering.
            /// </summary>
            /// <param name="builder">The parent builder.</param>
            protected ViewTypeLowering(IRBuilder builder)
                : this(builder.TypeContext)
            { }

            /// <summary>
            /// Constructs a new type lowering.
            /// </summary>
            /// <param name="typeContext">The parent type context.</param>
            protected ViewTypeLowering(IRTypeContext typeContext)
                : base(typeContext)
            { }

            /// <summary cref="TypeLowering{TType}.IsTypeDependent(TypeNode)"/>
            public override bool IsTypeDependent(TypeNode type) =>
                type.HasFlags(TypeFlags.ViewDependent);
        }

        /// <summary>
        /// Adds a set of rewriters specialized for a general view-type lowering.
        /// </summary>
        protected static void AddRewriters(
            Rewriter<TypeLowering<ViewType>> rewriter,
            RewriteConverter<
                TypeLowering<ViewType>, NewView> newViewConverter,
            RewriteConverter<
                TypeLowering<ViewType>, GetViewLength> getViewLengthConverter,
            RewriteConverter<
                TypeLowering<ViewType>, SubViewValue> subViewConverter,
            RewriteConverter<
                TypeLowering<ViewType>, AddressSpaceCast> addressSpaceCastConverter,
            RewriteConverter<
                TypeLowering<ViewType>, ViewCast> viewCastConverter,
            RewriteConverter<
                TypeLowering<ViewType>, LoadElementAddress> leaConverter,
            RewriteConverter<
                TypeLowering<ViewType>, AlignViewTo> alignToConverter)
        {
            AddRewriters(rewriter);

            rewriter.Add(Register, newViewConverter);
            rewriter.Add(Register, getViewLengthConverter);
            rewriter.Add(Register, subViewConverter);
            rewriter.Add(
                (converter, value) => value.IsViewCast && Register(converter, value),
                addressSpaceCastConverter);
            rewriter.Add(
                (converter, value) => Register(converter, value, value.SourceType),
                viewCastConverter);
            rewriter.Add(
                (converter, value) => value.IsViewAccess && Register(converter, value),
                leaConverter);
            rewriter.Add(
                (converter, value) => Register(converter, value, value.View.Type),
                alignToConverter);
        }

        /// <summary>
        /// Constructs a new view conversion pass.
        /// </summary>
        protected LowerViews() { }
    }
}
