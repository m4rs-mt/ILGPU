// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: TransformExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Resources;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents an abstract interface for a value transformer.
    /// </summary>
    /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
    /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
    public interface ITransformer<TSource, TTarget>
        where TSource : struct
        where TTarget : struct
    {
        /// <summary>
        /// Transforms the given value of type <typeparamref name="TSource"/>
        /// into a transformed value of type <typeparamref name="TTarget"/>.
        /// </summary>
        /// <param name="value">The value to transform.</param>
        /// <returns>
        /// The transformed value of type <typeparamref name="TTarget"/>.
        /// </returns>
        TTarget Transform(TSource value);
    }

    /// <summary>
    /// Represents a generic identity transformer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct IdentityTransformer<T> : ITransformer<T, T>
        where T : struct
    {
        #region ITransformer

        /// <summary>
        /// Performs an identity transformation by returning the input value.
        /// </summary>
        /// <param name="value">The value to transform.</param>
        /// <returns>The unchanged input value.</returns>
        public T Transform(T value) => value;

        #endregion
    }

    /// <summary>
    /// Represents an element transformer that Transforms elements in the source view into
    /// elements in the target view using the given transformer.
    /// </summary>
    /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
    /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
    /// <typeparam name="TTransformer">
    /// The transformer to transform elements from the source type to the target type.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source elements to transform</param>
    /// <param name="target">
    /// The target elements that will contain the transformed values.
    /// </param>
    /// <param name="transformer">The used transformer.</param>
    public delegate void Transformer<TSource, TTarget, TTransformer>(
        AcceleratorStream stream,
        ArrayView<TSource> source,
        ArrayView<TTarget> target,
        TTransformer transformer)
        where TSource : unmanaged
        where TTarget : unmanaged
        where TTransformer : struct, ITransformer<TSource, TTarget>;

    /// <summary>
    /// Represents an element transformer that Transforms elements in the source view into
    /// elements in the target view using the given transformer.
    /// </summary>
    /// <typeparam name="TSource">The source value type of the transformation.</typeparam>
    /// <typeparam name="TSourceStride">The 1D stride of the source view.</typeparam>
    /// <typeparam name="TTarget">The target value type of the transformation.</typeparam>
    /// <typeparam name="TTargetStride">The 1D stride of the target view.</typeparam>
    /// <typeparam name="TTransformer">
    /// The transformer to transform elements from the source type to the target type.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="source">The source elements to transform</param>
    /// <param name="target">
    /// The target elements that will contain the transformed values.
    /// </param>
    /// <param name="transformer">The used transformer.</param>
    public delegate void Transformer<
        TSource,
        TSourceStride,
        TTarget,
        TTargetStride,
        TTransformer>(
        AcceleratorStream stream,
        ArrayView1D<TSource, TSourceStride> source,
        ArrayView1D<TTarget, TTargetStride> target,
        TTransformer transformer)
        where TSource : unmanaged
        where TSourceStride : struct, IStride1D
        where TTarget : unmanaged
        where TTargetStride : struct, IStride1D
        where TTransformer : struct, ITransformer<TSource, TTarget>;

    /// <summary>
    /// Transformer functionality for accelerators.
    /// </summary>
    public static class TransformExtensions
    {
        #region Transform Implementation

        /// <summary>
        /// A actual raw transform loop body.
        /// </summary>
        /// <typeparam name="TSource">The source element type.</typeparam>
        /// <typeparam name="TSourceStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TTarget">The target element type.</typeparam>
        /// <typeparam name="TTargetStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TTransformer">The type of the transformer to use.</typeparam>
        internal readonly struct TransformImplementation<
            TSource,
            TSourceStride,
            TTarget,
            TTargetStride,
            TTransformer> : IGridStrideKernelBody
            where TSource : unmanaged
            where TSourceStride : struct, IStride1D
            where TTarget : unmanaged
            where TTargetStride : struct, IStride1D
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            /// <summary>
            /// Creates a new transform instance.
            /// </summary>
            /// <param name="source">The parent source view.</param>
            /// <param name="target">The parent target view.</param>
            /// <param name="transformer">The transformer instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TransformImplementation(
                ArrayView1D<TSource, TSourceStride> source,
                ArrayView1D<TTarget, TTargetStride> target,
                TTransformer transformer)
            {
                Source = source;
                Target = target;
                Transformer = transformer;
            }

            /// <summary>
            /// Returns the source view.
            /// </summary>
            public ArrayView1D<TSource, TSourceStride> Source { get; }

            /// <summary>
            /// Returns the target view.
            /// </summary>
            public ArrayView1D<TTarget, TTargetStride> Target { get; }

            /// <summary>
            /// Returns the transformer instance.
            /// </summary>
            public TTransformer Transformer { get; }

            /// <summary>
            /// Executes this transform wrapper.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Execute(LongIndex1D linearIndex)
            {
                if (linearIndex >= Source.Length)
                    return;

                Target[linearIndex] = Transformer.Transform(Source[linearIndex]);
            }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Finish() { }
        }

        /// <summary>
        /// Creates a raw transformer that is defined by the given source and target type
        /// and the specified transformer type.
        /// </summary>
        /// <typeparam name="TSource">
        /// The source value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TSourceStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TTarget">
        /// The target value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TTargetStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        private static Action<
            AcceleratorStream,
            LongIndex1D,
            TransformImplementation<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>>
            CreateRawTransformer<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>(
            this Accelerator accelerator)
            where TSource : unmanaged
            where TSourceStride : struct, IStride1D
            where TTarget : unmanaged
            where TTargetStride : struct, IStride1D
            where TTransformer : struct, ITransformer<TSource, TTarget> =>
            accelerator.LoadGridStrideKernel<TransformImplementation<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>>();

        #endregion

        /// <summary>
        /// Creates a raw transformer that is defined by the given source and target type
        /// and the specified transformer type.
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
        public static Transformer<TSource, TTarget, TTransformer>
            CreateTransformer<TSource, TTarget, TTransformer>(
            this Accelerator accelerator)
            where TSource : unmanaged
            where TTarget : unmanaged
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            var transformerLauncher = accelerator.CreateTransformer<
                TSource,
                Stride1D.Dense,
                TTarget,
                Stride1D.Dense,
                TTransformer>();
            return (stream, source, target, transformer) =>
                transformerLauncher(stream, source, target, transformer);
        }

        /// <summary>
        /// Creates a raw transformer that is defined by the given source and target type
        /// and the specified transformer type.
        /// </summary>
        /// <typeparam name="TSource">
        /// The source value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TSourceStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TTarget">
        /// The target value type of the transformation.
        /// </typeparam>
        /// <typeparam name="TTargetStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        public static Transformer<
            TSource,
            TSourceStride,
            TTarget,
            TTargetStride,
            TTransformer>
            CreateTransformer<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>(
            this Accelerator accelerator)
            where TSource : unmanaged
            where TSourceStride : struct, IStride1D
            where TTarget : unmanaged
            where TTargetStride : struct, IStride1D
            where TTransformer : struct, ITransformer<TSource, TTarget>
        {
            var rawTransformer = accelerator.CreateRawTransformer<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>();
            return (stream, source, target, transformer) =>
            {
                if (!source.IsValid)
                    throw new ArgumentNullException(nameof(source));
                if (source.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(source));
                if (!target.IsValid)
                    throw new ArgumentNullException(nameof(source));
                if (target.Length < source.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(target),
                        string.Format(
                            ErrorMessages.ViewOutOfRange,
                            nameof(source),
                            nameof(target)));
                }
                rawTransformer(
                    stream,
                    source.Length,
                    new TransformImplementation<
                        TSource,
                        TSourceStride,
                        TTarget,
                        TTargetStride,
                        TTransformer>(
                        source,
                        target,
                        transformer));
            };
        }

        /// <summary>
        /// Creates a new transformer that is defined by the element type and the
        /// specified transformer type.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The loaded transformer.</returns>
        public static Transformer<T, T, TTransformer> CreateTransformer<T, TTransformer>(
            this Accelerator accelerator)
            where T : unmanaged
            where TTransformer : struct, ITransformer<T, T> =>
            accelerator.CreateTransformer<T, T, TTransformer>();

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using
        /// the given transformer.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void Transform<T, TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> source,
            ArrayView<T> target,
            TTransformer transformer)
            where T : unmanaged
            where TTransformer : struct, ITransformer<T, T> =>
            accelerator.CreateTransformer<T, TTransformer>()(
                stream,
                source,
                target,
                transformer);

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using
        /// the given transformer.
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
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void Transform<TSource, TTarget, TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<TSource> source,
            ArrayView<TTarget> target,
            TTransformer transformer)
            where TSource : unmanaged
            where TTarget : unmanaged
            where TTransformer : struct, ITransformer<TSource, TTarget> =>
            accelerator.CreateTransformer<TSource, TTarget, TTransformer>()(
                stream,
                source,
                target,
                transformer);

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using
        /// the given transformer.
        /// </summary>
        /// <typeparam name="T">The type of the elements to transform.</typeparam>
        /// <typeparam name="TStride">The 1D stride of all views.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void Transform<T, TStride, TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> source,
            ArrayView1D<T, TStride> target,
            TTransformer transformer)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TTransformer : struct, ITransformer<T, T> =>
            accelerator.CreateTransformer<T, TStride, T, TStride, TTransformer>()(
                stream,
                source,
                target,
                transformer);

        /// <summary>
        /// Transforms elements in the source view into elements in the target view using
        /// the given transformer.
        /// </summary>
        /// <typeparam name="TSource">
        /// The source type of the elements to transform.
        /// </typeparam>
        /// <typeparam name="TSourceStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TTarget">
        /// The target type of the elements that have been transformed.
        /// </typeparam>
        /// <typeparam name="TTargetStride">The 1D stride of the target view.</typeparam>
        /// <typeparam name="TTransformer">
        /// The transformer to transform elements from the source type to the target type.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="source">The source elements to transform</param>
        /// <param name="target">
        /// The target elements that will contain the transformed values.
        /// </param>
        /// <param name="transformer">The used transformer.</param>
        public static void Transform<
            TSource,
            TSourceStride,
            TTarget,
            TTargetStride,
            TTransformer>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<TSource, TSourceStride> source,
            ArrayView1D<TTarget, TTargetStride> target,
            TTransformer transformer)
            where TSource : unmanaged
            where TSourceStride : struct, IStride1D
            where TTarget : unmanaged
            where TTargetStride : struct, IStride1D
            where TTransformer : struct, ITransformer<TSource, TTarget> =>
            accelerator.CreateTransformer<
                TSource,
                TSourceStride,
                TTarget,
                TTargetStride,
                TTransformer>()(
                stream,
                source,
                target,
                transformer);
    }
}
