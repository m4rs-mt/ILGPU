// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Construction;
using ILGPUC.IR.Rewriting;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers views (values and types) into platform specific instances.
/// </summary>
abstract class LowerViews : LowerTypes<ViewType>
{
    /// <summary>
    /// An abstract view type converter.
    /// </summary>
    /// <remarks>
    /// Constructs a new type lowering.
    /// </remarks>
    /// <param name="typeContext">The parent type context.</param>
    protected abstract class ViewTypeLowering(IRTypeContext typeContext) :
        TypeLowering<ViewType>(typeContext)
    {
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
            TypeLowering<ViewType>, AlignTo> alignToConverter,
        RewriteConverter<
            TypeLowering<ViewType>, AsAligned> asAlignedConverter)
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
            (converter, value) => value.IsViewOperation &&
                Register(converter, value, value.Source.Type),
            alignToConverter);
        rewriter.Add(
            (converter, value) => value.IsViewOperation &&
                Register(converter, value, value.Source.Type),
            asAlignedConverter);
    }
}
