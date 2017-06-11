// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReorderProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a transformer that is used for reordering and transforming
    /// elements of type <typeparamref name="TSource"/> to elements of type
    /// <typeparamref name="TTarget"/> using a transformer of type <typeparamref name="TTransformer"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type of the elements to transform.</typeparam>
    /// <typeparam name="TTarget">The target type of the elements that have been transformed.</typeparam>
    /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "This struct should be as generic as possible")]
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ReorderTransformWrapper<TSource, TTarget, TTransformer> : ITransformer<Index, TTarget>
        where TSource : struct
        where TTarget : struct
        where TTransformer : ITransformer<TSource, TTarget>
    {
        #region Instance

        private readonly ArrayView<TSource> sourceView;
        private readonly TTransformer transformer;

        /// <summary>
        /// Constructs a new reorder transformer.
        /// </summary>
        /// <param name="sourceView">The source elements.</param>
        /// <param name="transformer">The used transformer.</param>
        public ReorderTransformWrapper(
            ArrayView<TSource> sourceView,
            TTransformer transformer)
        {
            this.sourceView = sourceView;
            this.transformer = transformer;
        }

        #endregion

        #region ITransformer

        /// <summary cref="ITransformer{TSource, TTarget}.Transform(TSource)"/>
        public TTarget Transform(Index value)
        {
            var sourceValue = sourceView[value];
            return transformer.Transform(sourceValue);
        }

        #endregion
    }

    /// <summary>
    /// Reorders and transforms elements in the source view by storing the reordered elements in the target view.
    /// The values are reordered according to: target(idx) = transform(source(reorderView(idx))).
    /// </summary>
    /// <typeparam name="TSource">The source type of the elements to reorder and transform.</typeparam>
    /// <typeparam name="TTarget">The target type of the elements that have been transformed.</typeparam>
    /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source elements to transform</param>
    /// <param name="target">The target elements that will contain the transformed values.</param>
    /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
    /// <param name="transformer">The used transformer.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a transformation")]
    public delegate void ReorderTransformer<TSource, TTarget, TTransformer>(
        AcceleratorStream stream,
        ArrayView<TSource> source,
        ArrayView<TTarget> target,
        ArrayView<Index> reorderView,
        TTransformer transformer)
        where TSource : struct
        where TTarget : struct
        where TTransformer : struct, ITransformer<TSource, TTarget>;

    /// <summary>
    /// Reorder functionality for lightning contexts.
    /// </summary>
    public static class ReorderExtensions
    {
        #region Reorder

        /// <summary>
        /// Creates a reorder transformer that is defined by the given source and target type and the specified
        /// transformer type.
        /// </summary>
        /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
        /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <returns>The loaded transformer.</returns>
        public static ReorderTransformer<TSource, TTarget, TTransformer> CreateReorderTransformer<TSource, TTarget, TTransformer>(
            this LightningContext context)
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            var baseTransformer = context.CreateTransformer<Index, TTarget, ReorderTransformWrapper<TSource, TTarget, TTransformer>>();
            return (stream, source, target, reorderView, transformer) =>
            {
                if (!source.IsValid)
                    throw new ArgumentNullException(nameof(source));
                if (source.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(source));
                if (!reorderView.IsValid)
                    throw new ArgumentNullException(nameof(reorderView));
                if (reorderView.Length < source.Length)
                    throw new ArgumentOutOfRangeException(nameof(reorderView));
                baseTransformer(stream, reorderView, target, new ReorderTransformWrapper<TSource, TTarget, TTransformer>(source, transformer));
            };
        }

        /// <summary>
        /// Reorders elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = source(reorderView(idx)).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = source(reorderView(idx)).</param>
        public static void Reorder<T>(
            this LightningContext context,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView)
            where T : struct
        {
            context.Reorder(context.DefaultStream, source, target, reorderView);
        }

        /// <summary>
        /// Reorders elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = source(reorderView(idx)).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = source(reorderView(idx)).</param>
        public static void Reorder<T>(
            this LightningContext context,
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView)
            where T : struct
        {
            context.ReorderTransform<T, IdentityTransformer<T>>(
                stream,
                source,
                target,
                reorderView,
                new IdentityTransformer<T>());
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder and transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        public static void ReorderTransform<T, TTransformer>(
            this LightningContext context,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            context.ReorderTransform<T, T, TTransformer>(
                context.DefaultStream,
                source,
                target,
                reorderView,
                transformer);
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder and transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        public static void ReorderTransform<T, TTransformer>(
            this LightningContext context,
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            context.ReorderTransform<T, T, TTransformer>(
                stream,
                source,
                target,
                reorderView,
                transformer);
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="TSource">The source type of the elements to reorder and transform.</typeparam>
        /// <typeparam name="TTarget">The target type of the elements that have been transformed.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="context">The lightning context.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        public static void ReorderTransform<TSource, TTarget, TTransformer>(
            this LightningContext context,
            AcceleratorStream stream,
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            context.Transform(
                stream,
                reorderView,
                target,
                new ReorderTransformWrapper<TSource, TTarget, TTransformer>(source, transformer));
        }


        #endregion
    }
}
