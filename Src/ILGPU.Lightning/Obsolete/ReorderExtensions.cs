// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReorderProvider.cs (obsolete)
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        /// <summary>
        /// Creates a reorder transformer that is defined by the given source and target type and the specified
        /// transformer type.
        /// </summary>
        /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
        /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <returns>The loaded transformer.</returns>
        [Obsolete("Use Accelerator.CreateReorderTransformer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public ReorderTransformer<TSource, TTarget, TTransformer> CreateReorderTransformer<TSource, TTarget, TTransformer>()
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            return Accelerator.CreateReorderTransformer<TSource, TTarget, TTransformer>();
        }

        /// <summary>
        /// Reorders elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = source(reorderView(idx)).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder.</typeparam>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = source(reorderView(idx)).</param>
        [Obsolete("Use Accelerator.Reorder. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reorder<T>(
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView)
            where T : struct
        {
            Accelerator.Reorder(source, target, reorderView);
        }

        /// <summary>
        /// Reorders elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = source(reorderView(idx)).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = source(reorderView(idx)).</param>
        [Obsolete("Use Accelerator.Reorder. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reorder<T>(
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView)
            where T : struct
        {
            Accelerator.Reorder(
                stream,
                source,
                target,
                reorderView);
        }

        /// <summary>
        /// Reorders and transforms elements in the source view by storing the reordered elements in the target view.
        /// The values are reordered according to: target(idx) = transform(source(reorderView(idx))).
        /// </summary>
        /// <typeparam name="T">The type of the elements to reorder and transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.ReorderTransform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void ReorderTransform<T, TTransformer>(
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            Accelerator.ReorderTransform(
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
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.ReorderTransform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void ReorderTransform<T, TTransformer>(
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            Accelerator.ReorderTransform<T, T, TTransformer>(
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
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="reorderView">The view of indices such that target(idx) = transform(source(reorderView(idx))).</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.ReorderTransform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void ReorderTransform<TSource, TTarget, TTransformer>(
            AcceleratorStream stream,
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            ArrayView<Index> reorderView,
            TTransformer transformer)
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            Accelerator.ReorderTransform(stream, source, target, reorderView, transformer);
        }
    }
}
