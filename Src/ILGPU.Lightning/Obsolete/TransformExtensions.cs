// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: TransformExtensions.cs (obsolete)
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
        /// Creates a raw transformer that is defined by the given source and target type and the specified
        /// transformer type.
        /// </summary>
        /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
        /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <returns>The loaded transformer.</returns>
        [Obsolete("Use Accelerator.CreateTransformer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Transformer<TSource, TTarget, TTransformer> CreateTransformer<TSource, TTarget, TTransformer>()
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            return Accelerator.CreateTransformer<TSource, TTarget, TTransformer>();
        }

        /// <summary>
        /// Creates a new transformer that is defined by the element type and the specified transformer type.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <returns>The loaded transformer.</returns>
        [Obsolete("Use Accelerator.CreateTransformer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Transformer<T, T, TTransformer> CreateTransformer<T, TTransformer>()
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            return Accelerator.CreateTransformer<T, T, TTransformer>();
        }

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using the given transformer.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.Transform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Transform<T, TTransformer>(
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            Accelerator.Transform<T, TTransformer>(
                stream,
                source,
                target,
                transformer);
        }

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using the given transformer.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.Transform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Transform<T, TTransformer>(
            ArrayView<T> source,
            ArrayView<T> target,
            TTransformer transformer)
            where T : struct
            where TTransformer : struct, ITransformer<T, T>
        {
            Accelerator.Transform<T, TTransformer>(source, target, transformer);
        }

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using the given transformer.
        /// </summary>
        /// <typeparam name="TSource">The source type of the elements to transform.</typeparam>
        /// <typeparam name="TTarget">The target type of the elements that have been transformed.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.Transform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Transform<TSource, TTarget, TTransformer>(
            AcceleratorStream stream,
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            TTransformer transformer)
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            Accelerator.Transform(
                stream,
                source,
                target,
                transformer);
        }

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using the given transformer.
        /// </summary>
        /// <typeparam name="TSource">The source type of the elements to transform.</typeparam>
        /// <typeparam name="TTarget">The target type of the elements that have been transformed.</typeparam>
        /// <typeparam name="TTransformer">The transformer to transform elements from the source type to the target type.</typeparam>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">The target elements that will contain the transformed values.</param>
        /// <param name="transformer">The used transformer.</param>
        [Obsolete("Use Accelerator.Transform. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Transform<TSource, TTarget, TTransformer>(
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            TTransformer transformer)
            where TSource : struct
            where TTarget : struct
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            Accelerator.Transform(source, target, transformer);
        }
    }
}
