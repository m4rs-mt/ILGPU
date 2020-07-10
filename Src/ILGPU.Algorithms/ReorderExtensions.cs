// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: ReorderProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Runtime.InteropServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Reorders and transforms elements in the source view by storing the reordered
    /// elements in the target view. The values are reordered according to:
    /// target(idx) = transform(source(reorderView(idx))).
    /// </summary>
    /// <typeparam name="TSource">
    /// The source type of the elements to reorder and transform.
    /// </typeparam>
    /// <typeparam name="TTarget">
    /// The target type of the elements that have been transformed.
    /// </typeparam>
    /// <typeparam name="TTransformer">
    /// The transformer to transform elements from the source type to the target type.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source elements to transform</param>
    /// <param name="target">
    /// The target elements that will contain the transformed values.
    /// </param>
    /// <param name="reorderView">
    /// The view of indices such that target(idx) = transform(source(reorderView(idx))).
    /// </param>
    /// <param name="transformer">The used transformer.</param>
    public delegate void ReorderTransformer<TSource, TTarget, TTransformer>(
        AcceleratorStream stream,
        ArrayView<TSource> source,
        ArrayView<TTarget> target,
        ArrayView<Index1> reorderView,
        TTransformer transformer)
        where TSource : unmanaged
        where TTarget : unmanaged
        where TTransformer : struct, ITransformer<TSource, TTarget>;

    /// <summary>
    /// Reorder functionality for accelerators.
    /// </summary>
    public static class ReorderExtensions
    {
        #region Nested Types

        /// <summary>
        /// Represents a transformer that is used for reordering and transforming
        /// elements of type <typeparamref name="TSource"/> to elements of type
        /// <typeparamref name="TTarget"/> using a transformer of type
        /// <typeparamref name="TTransformer"/>.
        /// </summary>
        /// <typeparam name="TSource">
        /// The source type of the elements to transform.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// The target type of the elements that have been transformed.
        /// </typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal readonly struct ReorderTransformWrapper<TSource, TTarget, TTransformer> :
            ITransformer<Index1, TTarget>
            where TSource : unmanaged
            where TTarget : unmanaged
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            #region Instance

            /// <summary>
            /// Constructs a new reorder transformer.
            /// </summary>
            /// <param name="view">The source elements.</param>
            /// <param name="transformer">The used transformer.</param>
            public ReorderTransformWrapper(
                ArrayView<TSource> view,
                in TTransformer transformer)
            {
                SourceView = view;
                Transformer = transformer;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the source view.
            /// </summary>
            public ArrayView<TSource> SourceView { get; }

            /// <summary>
            /// Returns the underlying transformer.
            /// </summary>
            public TTransformer Transformer { get; }

            #endregion

            #region ITransformer

            /// <summary cref="ITransformer{TSource, TTarget}.Transform(TSource)"/>
            public readonly TTarget Transform(Index1 value)
            {
                var sourceValue = SourceView[value];
                return Transformer.Transform(sourceValue);
            }

            #endregion
        }

        #endregion

        #region Reorder

        /// <summary>
        /// Creates a reorder transformer that is defined by the given source and target
        /// type and the specified transformer type.
        /// </summary>
        /// <typeparam name="TSource">
        /// The source value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// The target value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        public static ReorderTransformer<TSource, TTarget, TTransformer>
            CreateReorderTransformer<
            TSource,
            TTarget,
            TTransformer>(
            this Accelerator accelerator)
            where TSource : unmanaged
            where TTarget : unmanaged
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            var baseTransformer = accelerator.CreateTransformer<
                Index1,
                TTarget,
                ReorderTransformWrapper<TSource, TTarget, TTransformer>>();
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
                baseTransformer(stream, reorderView, target,
                    new ReorderTransformWrapper<TSource, TTarget, TTransformer>(
                        source,
                        transformer));
            };
        }

        /// <summary>
        /// Reorders elements in the source view by storing the reordered elements in the
        /// target view. The values are reordered according to:
        /// target(idx) = source(reorderView(idx)).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="reorderView">
        /// The view of indices such that target(idx) = source(reorderView(idx)).
        /// </param>
        public static void Reorder<T>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index1> reorderView)
            where T : unmanaged
        {
            accelerator.ReorderTransform<T, IdentityTransformer<T>>(
                stream,
                source,
                target,
                reorderView,
                new IdentityTransformer<T>());
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered
        /// elements in the target view. The values are reordered according to:
        /// target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="T">
        /// The type of the elements to reorder and transform.
        /// </typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="reorderView">
        /// The view of indices such that target(idx) =
        /// transform(source(reorderView(idx))).
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void ReorderTransform<T, TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index1> reorderView,
            TTransformer transformer)
            where T : unmanaged
            where TTransformer : struct, ITransformer<T, T>
        {
            accelerator.ReorderTransform<T, T, TTransformer>(
                stream,
                source,
                target,
                reorderView,
                transformer);
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered
        /// elements in the target view. The values are reordered according to:
        /// target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="TSource">
        /// The source type of the elements to reorder and transform.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// The target type of the elements that have been transformed.
        /// </typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="reorderView">
        /// The view of indices such that target(idx) =
        /// transform(source(reorderView(idx))).
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void ReorderTransform<TSource, TTarget, TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            ArrayView<Index1> reorderView,
            TTransformer transformer)
            where TSource : unmanaged
            where TTarget : unmanaged
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            accelerator.Transform(
                stream,
                reorderView,
                target,
                new ReorderTransformWrapper<TSource, TTarget, TTransformer>(
                    source,
                    transformer));
        }

        #endregion
    }
}
