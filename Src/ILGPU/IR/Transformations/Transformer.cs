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

using ILGPU.IR.Values;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <param name="iteration">The current iteration.</param>
        void BeforeTransformation(
            IRContext context,
            Transformation transformation,
            int iteration);

        /// <summary>
        /// Will be invoked after a transformation has been applied.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="transformation">The applied transformation.</param>
        /// <param name="iteration">The current iteration.</param>
        void AfterTransformation(
            IRContext context,
            Transformation transformation,
            int iteration);
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
            TopLevelFunctionTransformationFlags.None,
            true);

        /// <summary>
        /// Represents a default configuration that works on all non-transformed functions
        /// and marks them as transformed.
        /// </summary>
        public static readonly TransformerConfiguration Transformed = new TransformerConfiguration(
            TopLevelFunctionTransformationFlags.Transformed,
            true);

        /// <summary>
        /// Constructs a new transformer configuration.
        /// </summary>
        /// <param name="flags">The transformation flags.</param>
        /// <param name="finalGC">True, if a final GC run is required.</param>
        public TransformerConfiguration(
            TopLevelFunctionTransformationFlags flags,
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
            TopLevelFunctionTransformationFlags requiredFlags,
            TopLevelFunctionTransformationFlags flags,
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
        public TopLevelFunctionTransformationFlags RequiredFlags { get; }

        /// <summary>
        /// Returns the transformation flags that will be stored on
        /// on the transformed functions.
        /// </summary>
        public TopLevelFunctionTransformationFlags TransformationFlags { get; }

        /// <summary>
        /// Returns true if the current configuration manipulates transformation flags.
        /// </summary>
        public bool AddsFlags => TransformationFlags != TopLevelFunctionTransformationFlags.None;
    }

    /// <summary>
    /// Applies transformations to contexts.
    /// </summary>
    public sealed class Transformer
    {
        #region Nested Types

        /// <summary>
        /// A transformer builder.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private ImmutableArray<TransformSpecification>.Builder builder;

            /// <summary>
            /// Constructs a new builder.
            /// </summary>
            /// <param name="configuration">The transformer configuration.</param>
            /// <param name="targetBuilder">The target builder.</param>
            internal Builder(
                TransformerConfiguration configuration,
                ImmutableArray<TransformSpecification>.Builder targetBuilder)
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
                Add(new TransformSpecification(transformation));
            }

            /// <summary>
            /// Adds the given transformation to the manager.
            /// </summary>
            /// <param name="transformation">The transformation to add.</param>
            /// <param name="maxNumIterations">The maximum number of iterations for this transformation.</param>
            public void Add(Transformation transformation, int maxNumIterations)
            {
                Add(new TransformSpecification(
                    transformation,
                    maxNumIterations));
            }

            /// <summary>
            /// Adds the given transformation to the manager.
            /// </summary>
            /// <param name="specification">The specification to add.</param>
            public void Add(TransformSpecification specification)
            {
                builder.Add(specification);
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
        /// Represents a specification of a single transformation.
        /// </summary>
        public readonly struct TransformSpecification
        {
            /// <summary>
            /// Constructs a new specification.
            /// </summary>
            /// <param name="transformation">The transformation.</param>
            public TransformSpecification(Transformation transformation)
                : this(transformation, 1)
            { }

            /// <summary>
            /// Constructs a new specification.
            /// </summary>
            /// <param name="transformation">The transformation.</param>
            /// <param name="maxNumIterations">The desired maximum number of iteration.</param>
            public TransformSpecification(
                Transformation transformation,
                int maxNumIterations)
            {
                if (maxNumIterations < 1)
                    throw new ArgumentOutOfRangeException(nameof(maxNumIterations));
                Transformation = transformation ?? throw new ArgumentNullException(nameof(transformation));
                MaxNumIterations = maxNumIterations;
            }

            /// <summary>
            /// Returns the associated transformation.
            /// </summary>
            public Transformation Transformation { get; }

            /// <summary>
            /// Returns the maximum number of iterations.
            /// </summary>
            public int MaxNumIterations { get; }

            /// <summary>
            /// Returns the string representation of this specification.
            /// </summary>
            /// <returns>Returns the string representation of this specification.</returns>
            public override string ToString()
            {
                return $"{Transformation} [{MaxNumIterations}]";
            }

            /// <summary>
            /// Converts the given transformation to a default transformation specification.
            /// </summary>
            /// <param name="transformation">The transformation to convert.</param>
            public static implicit operator TransformSpecification(Transformation transformation)
            {
                return new TransformSpecification(transformation);
            }
        }

        /// <summary>
        /// Represents no transformer handler.
        /// </summary>
        private readonly struct NoHandler : ITransformerHandler
        {
            /// <summary cref="ITransformerHandler.BeforeTransformation(IRContext, Transformation, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void BeforeTransformation(
                IRContext context,
                Transformation transformation,
                int iteration) { }

            /// <summary cref="ITransformerHandler.AfterTransformation(IRContext, Transformation, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AfterTransformation(
                IRContext context,
                Transformation transformation,
                int iteration) { }
        }

        /// <summary>
        /// Represents an implementation of an <see cref="ITransformationManager"/>
        /// </summary>
        private readonly struct TransformationManager : ITransformationManager
        {
            /// <summary>
            /// Constructs a new transformation flags container.
            /// </summary>
            /// <param name="transformer">The parent transformer.</param>
            public TransformationManager(Transformer transformer)
            {
                FlagsMapping = transformer.flagsMapping;
            }

            /// <summary>
            /// Returns the current mapping dictionary.
            /// </summary>
            public ConcurrentDictionary<TopLevelFunction, TransformationFlags> FlagsMapping { get; }

            /// <summary cref="ITransformationManager.SuccessfullyTransformed(TopLevelFunction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SuccessfullyTransformed(TopLevelFunction topLevelFunction)
            {
                topLevelFunction.AddTransformationFlags(
                    TopLevelFunctionTransformationFlags.Dirty);
            }

            /// <summary cref="ITransformationManager.HasTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags)
            {
                if (!FlagsMapping.TryGetValue(topLevelFunction, out TransformationFlags currentFlags))
                    return false;
                return (currentFlags & flags) == flags;
            }

            /// <summary cref="ITransformationManager.AddTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags)
            {
                if (!FlagsMapping.TryGetValue(topLevelFunction, out TransformationFlags currentFlags))
                    currentFlags = TransformationFlags.None;
                FlagsMapping[topLevelFunction] = currentFlags | flags;
            }

            /// <summary cref="ITransformationManager.RemoveTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags)
            {
                if (!FlagsMapping.TryGetValue(topLevelFunction, out TransformationFlags currentFlags))
                    return;
                FlagsMapping[topLevelFunction] = currentFlags & ~flags;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Update(IRContext context)
            {
                var tempStorage = new List<KeyValuePair<TopLevelFunction, TransformationFlags>>(FlagsMapping.Count);
                foreach (var entry in FlagsMapping)
                    tempStorage.Add(entry);

                FlagsMapping.Clear();
                foreach (var entry in tempStorage)
                {
                    var function = entry.Key;
                    context.RefreshFunction(ref function);
                    FlagsMapping[function] = entry.Value;
                }
            }
        }

        /// <summary>
        /// Represents a function predicate for functions to transform.
        /// </summary>
        private readonly struct FunctionPredicate : IFunctionCollectionPredicate
        {
            /// <summary>
            /// Constructs a new function predicate.
            /// </summary>
            /// <param name="flags">The desired flags that should not be set.</param>
            public FunctionPredicate(TopLevelFunctionTransformationFlags flags)
            {
                Flags = flags;
            }

            /// <summary>
            /// Returns the flags that should not be set on the target function.
            /// </summary>
            public TopLevelFunctionTransformationFlags Flags { get; }

            /// <summary cref="IFunctionCollectionPredicate.Match(TopLevelFunction)"/>
            public bool Match(TopLevelFunction topLevelFunction) =>
                (topLevelFunction.TransformationFlags & Flags) == TopLevelFunctionTransformationFlags.None;
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new transformer builder.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <returns>A new builder.</returns>
        public static Builder CreateBuilder(TransformerConfiguration configuration) =>
            new Builder(configuration, ImmutableArray.CreateBuilder<TransformSpecification>());

        /// <summary>
        /// Creates a transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transform">The specification to use.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer Create(
            TransformerConfiguration configuration,
            TransformSpecification transform) =>
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
            TransformSpecification transform,
            params TransformSpecification[] transformations)
        {
            var builder = ImmutableArray.CreateBuilder<TransformSpecification>(
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
            ImmutableArray<TransformSpecification> transforms) =>
            new Transformer(configuration, transforms);

        #endregion

        #region Instance

        /// <summary>
        /// The main synchronization object.
        /// </summary>
        private readonly object syncRoot = new object();

        private readonly int[] performedIterations;
        private readonly ConcurrentDictionary<TopLevelFunction, TransformationFlags> flagsMapping =
            new ConcurrentDictionary<TopLevelFunction, TransformationFlags>();

        /// <summary>
        /// Constructs a new empty transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        private Transformer(TransformerConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Constructs a new transformer.
        /// </summary>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="transformations">The transformations.</param>
        private Transformer(
            TransformerConfiguration configuration,
            ImmutableArray<TransformSpecification> transformations)
            : this(configuration)
        {
            Debug.Assert(!transformations.IsEmpty, "Invalid number of transformations");

            Transformations = transformations;
            MaxNumIterations = 0;
            performedIterations = new int[transformations.Length];
            foreach (var entry in transformations)
                MaxNumIterations = Math.Max(entry.MaxNumIterations, MaxNumIterations);
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
        public ImmutableArray<TransformSpecification> Transformations { get; }

        /// <summary>
        /// Returns the currently known maximum number of iterations.
        /// </summary>
        public int MaxNumIterations { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Clones this transformer and returns the cloned instance.
        /// </summary>
        /// <returns>The cloned transformer.</returns>
        public Transformer Clone() => new Transformer(Configuration, Transformations);

        /// <summary>
        /// Applies all transformations to the given context.
        /// </summary>
        /// <param name="context">The target IR context.</param>
        public void Transform(IRContext context) =>
            Transform(context, int.MaxValue);

        /// <summary>
        /// Applies all transformations to the given context.
        /// </summary>
        /// <param name="context">The target IR context.</param>
        /// <param name="maxNumIterations">The maximum number of transformations.</param>
        public void Transform(IRContext context, int maxNumIterations) =>
            Transform(context, maxNumIterations, new NoHandler());

        /// <summary>
        /// Applies all transformations to the given context.
        /// </summary>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="context">The target IR context.</param>
        /// <param name="handler">The target handler.</param>
        public void Transform<THandler>(IRContext context, THandler handler)
            where THandler : ITransformerHandler
        {
            Transform(context, int.MaxValue, handler);
        }

        /// <summary>
        /// Performs a local GC process.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="manager">The current transformation manager.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PerformGC(IRContext context, ref TransformationManager manager)
        {
            context.GC();
            manager.Update(context);
        }

        /// <summary>
        /// Applies all transformations to the given context.
        /// </summary>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="context">The target IR context.</param>
        /// <param name="maxNumIterations">The maximum number of transformations.</param>
        /// <param name="handler">The target handler.</param>
        public void Transform<THandler>(
            IRContext context,
            int maxNumIterations,
            THandler handler)
            where THandler : ITransformerHandler
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (maxNumIterations < 1)
                throw new ArgumentOutOfRangeException(nameof(maxNumIterations));

            var numTransformations = Transformations.Length;
            if (numTransformations < 1)
                return;

            lock (syncRoot)
            {
                var functionsToTransform = context.GetUnsafeFunctionCollection(
                    new FunctionPredicate(Configuration.RequiredFlags));
                if (functionsToTransform.TotalNumFunctions < 1)
                    return;

                // Prepare local data
                flagsMapping.Clear();
                var manager = new TransformationManager(this);
                Array.Clear(performedIterations, 0, numTransformations);

                bool gcApplied = false;
                bool appliedTransformation = false;

                do
                {
                    bool continueProcessing = false;
                    for (int i = 0; i < numTransformations; ++i)
                    {
                        var transformEntry = Transformations[i];
                        var iterationCount = performedIterations[i];
                        if (iterationCount >= transformEntry.MaxNumIterations)
                            continue;
                        performedIterations[i] = iterationCount + 1;
                        var transform = transformEntry.Transformation;
                        if (i > 0 && transform.RequiresCleanIR && !gcApplied)
                        {
                            PerformGC(context, ref manager);
                            gcApplied = true;
                        }
                        handler.BeforeTransformation(
                            context,
                            transform,
                            iterationCount);
                        if (transform.Transform(functionsToTransform, manager))
                        {
                            continueProcessing = true;
                            gcApplied = false;
                            appliedTransformation = true;

                            if (transform.RequiresCleanupAferApplication)
                            {
                                PerformGC(context, ref manager);
                                gcApplied = true;
                            }
                        }
                        handler.AfterTransformation(
                            context,
                            transform,
                            iterationCount);
                    }
                    if (!continueProcessing)
                        break;
                    --maxNumIterations;
                }
                while (maxNumIterations > 0);

                if (!gcApplied && appliedTransformation && Configuration.FinalGC)
                    context.GC();

                // Mark all functions as transformed
                if (Configuration.AddsFlags)
                {
                    foreach (var function in functionsToTransform)
                        function.AddTransformationFlags(Configuration.TransformationFlags);
                }
            }
        }

        #endregion
    }
}
