// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Transformer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a transformer configuration.
    /// </summary>
    public readonly struct TransformerConfiguration
    {
        /// <summary>
        /// Represents an empty configuration that works on all functions without
        /// adding additional flags to them.
        /// </summary>
        public static readonly TransformerConfiguration Empty =
            new TransformerConfiguration(MethodTransformationFlags.None);

        /// <summary>
        /// Represents a default configuration that works on all non-transformed
        /// functions and marks them as transformed.
        /// </summary>
        public static readonly TransformerConfiguration Transformed =
            new TransformerConfiguration(MethodTransformationFlags.Transformed);

        /// <summary>
        /// Constructs a new transformer configuration.
        /// </summary>
        /// <param name="flags">The transformation flags.</param>
        public TransformerConfiguration(MethodTransformationFlags flags)
            : this(flags, flags)
        { }

        /// <summary>
        /// Constructs a new transformer configuration.
        /// </summary>
        /// <param name="requiredFlags">
        /// The transformation flags that should not be set.
        /// </param>
        /// <param name="flags">The transformation flags that will be set.</param>
        public TransformerConfiguration(
            MethodTransformationFlags requiredFlags,
            MethodTransformationFlags flags)
        {
            RequiredFlags = requiredFlags;
            TransformationFlags = flags;
        }

        /// <summary>
        /// Returns the transformation flags that will be checked
        /// on the functions to transform.
        /// </summary>
        public MethodTransformationFlags RequiredFlags { get; }

        /// <summary>
        /// Returns the transformation flags that will be stored on
        /// on the transformed functions.
        /// </summary>
        public MethodTransformationFlags TransformationFlags { get; }

        /// <summary>
        /// Returns true if the current configuration manipulates transformation flags.
        /// </summary>
        public readonly bool AddsFlags =>
            TransformationFlags != MethodTransformationFlags.None;

        /// <summary>
        /// Returns a compatible collection predicate.
        /// </summary>
        public readonly MethodCollections.ToTransform Predicate =>
            new MethodCollections.ToTransform(RequiredFlags);
    }

    /// <summary>
    /// Applies transformations to contexts.
    /// </summary>
    public readonly struct Transformer
    {
        #region Nested Types

        /// <summary>
        /// A transformer builder.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private readonly ImmutableArray<Transformation>.Builder builder;

            /// <summary>
            /// Constructs a new builder.
            /// </summary>
            /// <param name="configuration">The transformer configuration.</param>
            /// <param name="targetBuilder">The target builder.</param>
            internal Builder(
                TransformerConfiguration configuration,
                ImmutableArray<Transformation>.Builder targetBuilder)
            {
                Configuration = configuration;
                builder = targetBuilder;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current transformer configuration.
            /// </summary>
            public TransformerConfiguration Configuration { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given transformation to the manager.
            /// </summary>
            /// <param name="transformation">The transformation to add.</param>
            public void Add(Transformation transformation) =>
                builder.Add(transformation
                            ?? throw new ArgumentNullException(nameof(transformation)));

            /// <summary>
            /// Converts this builder to an immutable array.
            /// </summary>
            /// <returns>The immutable transformation array.</returns>
            public Transformer ToTransformer()
            {
                var transformations = builder.ToImmutable();
                return new Transformer(Configuration, transformations);
            }

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new transformer builder.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <returns>A new builder.</returns>
        public static Builder CreateBuilder(TransformerConfiguration configuration) =>
            new Builder(
                configuration,
                ImmutableArray.CreateBuilder<Transformation>());

        /// <summary>
        /// Creates a transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transform">The specification to use.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer Create(
            TransformerConfiguration configuration,
            Transformation transform) =>
            Create(configuration, ImmutableArray.Create(transform));

        /// <summary>
        /// Creates a transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transform">The first transformation.</param>
        /// <param name="transformations">The other transformations.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer Create(
            TransformerConfiguration configuration,
            Transformation transform,
            params Transformation[] transformations)
        {
            var builder = ImmutableArray.CreateBuilder<Transformation>(
                transformations.Length + 1);
            builder.Add(transform);
            builder.AddRange(transformations);
            return Create(configuration, builder.MoveToImmutable());
        }

        /// <summary>
        /// Creates a transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transforms">The transformations.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer Create(
            TransformerConfiguration configuration,
            ImmutableArray<Transformation> transforms) =>
            new Transformer(configuration, transforms);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transformations">The transformations.</param>
        private Transformer(
            TransformerConfiguration configuration,
            ImmutableArray<Transformation> transformations)
        {
            Configuration = configuration;
            Transformations = transformations;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated configuration.
        /// </summary>
        public TransformerConfiguration Configuration { get; }

        /// <summary>
        /// Returns the stored transformations.
        /// </summary>
        public ImmutableArray<Transformation> Transformations { get; }

        #endregion
    }
}
