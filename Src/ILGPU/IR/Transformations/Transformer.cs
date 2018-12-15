// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Transformer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a transformer callback.
    /// </summary>
    public interface ITransformerHandler
    {
        /// <summary>
        /// Will be invoked before a transformation is applied.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="transformation">The transformation to apply.</param>
        void BeforeTransformation(
            IRContext context,
            Transformation transformation);

        /// <summary>
        /// Will be invoked after a transformation has been applied.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="transformation">The applied transformation.</param>
        void AfterTransformation(
            IRContext context,
            Transformation transformation);
    }

    /// <summary>
    /// Represents a transformer configuration.
    /// </summary>
    public readonly struct TransformerConfiguration
    {
        /// <summary>
        /// Represents an empty configuration that works on all functions without
        /// adding additional flags to them.
        /// </summary>
        public static readonly TransformerConfiguration Empty = new TransformerConfiguration(
            MethodTransformationFlags.None,
            true);

        /// <summary>
        /// Represents a default configuration that works on all non-transformed functions
        /// and marks them as transformed.
        /// </summary>
        public static readonly TransformerConfiguration Transformed = new TransformerConfiguration(
            MethodTransformationFlags.Transformed,
            true);

        /// <summary>
        /// Constructs a new transformer configuration.
        /// </summary>
        /// <param name="flags">The transformation flags.</param>
        /// <param name="finalGC">True, if a final GC run is required.</param>
        public TransformerConfiguration(
            MethodTransformationFlags flags,
            bool finalGC)
            : this(flags, flags, finalGC)
        { }

        /// <summary>
        /// Constructs a new transformer configuration.
        /// </summary>
        /// <param name="requiredFlags">The transformation flags that should not be set.</param>
        /// <param name="flags">The transformation flags that will be set.</param>
        /// <param name="finalGC">True, if a final GC run is required.</param>
        public TransformerConfiguration(
            MethodTransformationFlags requiredFlags,
            MethodTransformationFlags flags,
            bool finalGC)
        {
            RequiredFlags = requiredFlags;
            TransformationFlags = flags;
            FinalGC = finalGC;
        }

        /// <summary>
        /// Returns true if a final GC run is required.
        /// </summary>
        public bool FinalGC { get; }

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
        public bool AddsFlags => TransformationFlags != MethodTransformationFlags.None;
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

            private ImmutableArray<Transformation>.Builder builder;

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
            public void Add(Transformation transformation)
            {
                builder.Add(transformation ??
                    throw new ArgumentNullException(nameof(transformation)));
            }

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

        /// <summary>
        /// Represents a function predicate for functions to transform.
        /// </summary>
        private readonly struct MethodPredicate : IMethodCollectionPredicate
        {
            /// <summary>
            /// Constructs a new function predicate.
            /// </summary>
            /// <param name="flags">The desired flags that should not be set.</param>
            public MethodPredicate(MethodTransformationFlags flags)
            {
                Flags = flags;
            }

            /// <summary>
            /// Returns the flags that should not be set on the target function.
            /// </summary>
            public MethodTransformationFlags Flags { get; }

            /// <summary cref="IMethodCollectionPredicate.Match(Method)"/>
            public bool Match(Method method) =>
                (method.TransformationFlags & Flags) == MethodTransformationFlags.None;
        }

        #endregion

        #region Static

        /// <summary>
        /// Represents an empty transformer.
        /// </summary>
        public static readonly Transformer Empty = new Transformer(
            new TransformerConfiguration(MethodTransformationFlags.None, false),
            ImmutableArray<Transformation>.Empty);

        /// <summary>
        /// Creates a new transformer builder.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <returns>A new builder.</returns>
        public static Builder CreateBuilder(TransformerConfiguration configuration) =>
            new Builder(configuration, ImmutableArray.CreateBuilder<Transformation>());

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

        /// <summary>
        /// Returns the number of stored transformations.
        /// </summary>
        public int Length => Transformations.Length;

        #endregion

        #region Methods

        /// <summary>
        /// Applies all transformations to the given context.
        /// </summary>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="context">The target IR context.</param>
        /// <param name="handler">The target handler.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Transform<THandler>(
            IRContext context,
            THandler handler)
            where THandler : ITransformerHandler
        {
            Debug.Assert(context != null, "Invalid conext");

            var toTransform = context.GetMethodCollection(
                new MethodPredicate(Configuration.RequiredFlags));
            if (toTransform.TotalNumMethods < 1)
                return;

            // Apply all transformations
            foreach (var transform in Transformations)
            {
                handler.BeforeTransformation(
                    context,
                    transform);
                transform.Transform(toTransform);
                handler.AfterTransformation(
                    context,
                    transform);
            }

            // Apply final flags
            foreach (var entry in toTransform)
                entry.AddTransformationFlags(Configuration.TransformationFlags);

            if (Configuration.FinalGC)
                context.GC();
        }

        #endregion
    }
}
