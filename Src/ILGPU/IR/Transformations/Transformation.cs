// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Transformation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if PARALLEL_PROCESSING
using System.Threading;
using System.Threading.Tasks;
#endif

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Flags that are attachted to transformations and functions.
    /// Transformations use these flags to indicate whether a processing
    /// step is required or not.
    /// </summary>
    [Flags]
    [SuppressMessage("Microsoft.Usage", "CA2217: DoNotMarkEnumsWithFlags",
        Justification = "The All field simplifies activation/deactivation of all flags")]
    public enum TransformationFlags : int
    {
        /// <summary>
        /// The empty transformation flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Inlines functions.
        /// </summary>
        Inlining = 1 << 0,

        /// <summary>
        /// Optimizes parameters.
        /// </summary>
        OptimizeParameters = 1 << 1,

        /// <summary>
        /// Normalizes all call instructions.
        /// </summary>
        NormalizeCalls = 1 << 2,

        /// <summary>
        /// Simplifies control flow.
        /// </summary>
        MergeCallChains = 1 << 3,

        /// <summary>
        /// Merges nop calls that pass arguments to unecessary targets.
        /// </summary>
        MergeNopCalls = 1 << 4,

        /// <summary>
        /// Infers address spaces.
        /// </summary>
        InferAddressSpaces = 1 << 5,
        
        /// <summary>
        /// Transforms load-store operations into CPS parameters.
        /// </summary>
        TransformToCPS = 1 << 6,

        /// <summary>
        /// Destroys structures by turning them into scalar values.
        /// </summary>
        DestroyStructures = 1 << 7,

        /// <summary>
        /// Specializes built in views.
        /// </summary>
        SpecializeViews = 1 << 8,

        /// <summary>
        /// Specializes device-specific intrinsics.
        /// </summary>
        SpecializeIntrinsics = 1 << 9,

        /// <summary>
        /// Represents all transformation flags.
        /// </summary>
        All = 0x0fffffff,
    }

    /// <summary>
    /// Represents an abstract transformation manager.
    /// </summary>
    public interface ITransformationManager
    {
        /// <summary>
        /// Will be invoked if the given function was successfully transformed.
        /// </summary>
        /// <param name="topLevelFunction">The transformed function.</param>
        void SuccessfullyTransformed(TopLevelFunction topLevelFunction);

        /// <summary>
        /// Returns true iff the function has the given transformation flags.
        /// </summary>
        /// <param name="topLevelFunction">The function.</param>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, iff the function has the given transformation flags.</returns>
        bool HasTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags);

        /// <summary>
        /// Adds the given flags to the function.
        /// </summary>
        /// <param name="topLevelFunction">The function.</param>
        /// <param name="flags">The flags to add.</param>
        void AddTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags);

        /// <summary>
        /// Removes the given flags from the function.
        /// </summary>
        /// <param name="topLevelFunction">The function.</param>
        /// <param name="flags">The flags to remove.</param>
        void RemoveTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags);
    }

    /// <summary>
    /// Represents a generic transformation.
    /// </summary>
    public abstract class Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract transform execution driver closure.
        /// </summary>
        protected internal interface ITransformExecutor
        {
            /// <summary>
            /// Executes the current transformation.
            /// </summary>
            /// <param name="builder">The current IR builder.</param>
            /// <param name="topLevelFunction">The target top-level function.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            bool Execute(IRBuilder builder, TopLevelFunction topLevelFunction);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        protected Transformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags)
            : this(flags, followUpFlags, false, false)
        { }

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        /// <param name="requiresCleanIR">True, iff a previous GC run is required.</param>
        /// <param name="requiresCleanupAfterApplicaion">True, iff a GC run is required after a successfull application.</param>
        protected Transformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags,
            bool requiresCleanIR,
            bool requiresCleanupAfterApplicaion)
        {
            Flags = flags;
            DesiredTransformationFlags = followUpFlags;
            RequiresCleanIR = requiresCleanIR;
            RequiresCleanupAferApplication = requiresCleanupAfterApplicaion;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated transformation flags.
        /// </summary>
        public TransformationFlags Flags { get; }

        /// <summary>
        /// Returns the desired flags that indicate passes that should run
        /// on the marked function.
        /// </summary>
        public TransformationFlags DesiredTransformationFlags { get; }

        /// <summary>
        /// Returns the required transformation flags.
        /// </summary>
        public TransformationFlags RequiredTransformationFlags { get; protected set; }

        /// <summary>
        /// Returns true iff iff this transformation requires a previous GC run.
        /// </summary>
        public bool RequiresCleanIR { get; }

        /// <summary>
        /// Returns true iff this transformation requires a cleanup phase
        /// after an application.
        /// </summary>
        public bool RequiresCleanupAferApplication { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms all functions in the given context.
        /// </summary>
        /// <param name="functions">The functions to transform.</param>
        /// <param name="transformationManager">The current transformation-flags container</param>
        public abstract bool Transform<TPredicate, TManager>(
            UnsafeFunctionCollection<TPredicate> functions,
            TManager transformationManager)
            where TPredicate : IFunctionCollectionPredicate
            where TManager : ITransformationManager;

        /// <summary>
        /// Transforms the given top-level function using the provided builder while
        /// checking and updating the associated <see cref="Flags"/>.
        /// </summary>
        /// <param name="topLevelFunction">The current top-level function.</param>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="transformationManager">The current transformation-flags container</param>
        /// <param name="executor">The desired transform executor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal bool ExecuteTransform<TManager, TExecutor>(
            IRBuilder builder,
            TopLevelFunction topLevelFunction,
            TManager transformationManager,
            in TExecutor executor)
            where TManager : ITransformationManager
            where TExecutor : struct, ITransformExecutor
        {
            // Check whether this function has been processed or not
            if (transformationManager.HasTransformationFlags(topLevelFunction, Flags) ||
                RequiredTransformationFlags != TransformationFlags.None &&
                !transformationManager.HasTransformationFlags(topLevelFunction, RequiredTransformationFlags))
                return false;

            // Mark this function as processed
            transformationManager.AddTransformationFlags(topLevelFunction, Flags);

#if DEBUG
            try
            {
#endif
                if (executor.Execute(builder, topLevelFunction))
                {
                    // Mark function as transformed and ditry
                    transformationManager.SuccessfullyTransformed(topLevelFunction);

                    // Remove desired transformation flags since the associated functions
                    // should run on this function later on
                    transformationManager.RemoveTransformationFlags(topLevelFunction, DesiredTransformationFlags);
                    return true;
                }
#if DEBUG
            }
            catch (Exception)
            {
                throw;
            }
#endif
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that can be applied in an unordered manner.
    /// </summary>
    public abstract class UnorderedTransformation : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an unordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            public Executor(UnorderedTransformation parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public UnorderedTransformation Parent { get; }

            /// <summary cref="Transformation.ITransformExecutor.Execute(IRBuilder, TopLevelFunction)"/>
            public bool Execute(IRBuilder builder, TopLevelFunction topLevelFunction) =>
                Parent.PerformTransformation(builder, topLevelFunction);
        }

        /// <summary>
        /// Represents an empty implementation of a transformation flags container.
        /// </summary>
        private readonly struct NoTransformationFlagsContainer : ITransformationManager
        {
            /// <summary cref="ITransformationManager.SuccessfullyTransformed(TopLevelFunction)"/>
            public void SuccessfullyTransformed(TopLevelFunction topLevelFunction)
            {
                topLevelFunction.AddTransformationFlags(
                    TopLevelFunctionTransformationFlags.Dirty |
                    TopLevelFunctionTransformationFlags.Transformed);
            }

            /// <summary cref="ITransformationManager.HasTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            public bool HasTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags) => false;

            /// <summary cref="ITransformationManager.AddTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            public void AddTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags) { }

            /// <summary cref="ITransformationManager.RemoveTransformationFlags(TopLevelFunction, TransformationFlags)"/>
            public void RemoveTransformationFlags(TopLevelFunction topLevelFunction, TransformationFlags flags) { }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        protected UnorderedTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags)
            : base(flags, followUpFlags)
        { }

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        /// <param name="requiresCleanIR">True, iff a previous GC run is required.</param>
        /// <param name="requiresCleanupAfterApplication">True, iff a GC run is required after a successfull application.</param>
        protected UnorderedTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags,
            bool requiresCleanIR,
            bool requiresCleanupAfterApplication)
            : base(flags, followUpFlags, requiresCleanIR, requiresCleanupAfterApplication)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the given top-level function using the provided builder.
        /// </summary>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="topLevelFunction">The current top-level function.</param>
        public bool Transform(IRBuilder builder, TopLevelFunction topLevelFunction) =>
            ExecuteTransform(
                builder ?? throw new ArgumentNullException(nameof(builder)),
                topLevelFunction ?? throw new ArgumentNullException(nameof(topLevelFunction)),
                new NoTransformationFlagsContainer(),
                new Executor(this));

        /// <summary cref="Transformation.Transform{TPredicate, TManager}(UnsafeFunctionCollection{TPredicate}, TManager)"/>
        public sealed override bool Transform<TPredicate, TManager>(
            UnsafeFunctionCollection<TPredicate> functions,
            TManager transformationManager)
        {
            int result = 0;
            using (var irBuilder = functions.CreateBuilder(IRBuilderFlags.PreserveTopLevelFunctions))
            {
                var executor = new Executor(this);
#if !PARALLEL_PROCESSING
                foreach (var function in functions)
                {
                    if (ExecuteTransform(irBuilder, function, transformationManager, executor))
                        ++result;
                }
#else
                Parallel.ForEach(
                    functions,
                    function =>
                    {
                        if (ExecuteTransform(irBuilder, function, transformationManager, executor))
                            Interlocked.Add(ref result, 1);
                    });
#endif
            }
            return result != 0;
        }

        /// <summary>
        /// Transforms the given top-level function using the provided builder.
        /// </summary>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="topLevelFunction">The current top-level function.</param>
        protected abstract bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that will be applied in the post order
    /// of the induced call graph.
    /// </summary>
    public abstract class OrderedTransformation : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an ordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            /// <param name="functionLandscape">The current function landscape.</param>
            /// <param name="entry">The current landscape entry.</param>
            public Executor(
                OrderedTransformation parent,
                FunctionLandscape functionLandscape,
                FunctionLandscape.Entry entry)
            {
                Parent = parent;
                FunctionLandscape = functionLandscape;
                Entry = entry;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public OrderedTransformation Parent { get; }

            /// <summary>
            /// Returns the current landscape.
            /// </summary>
            public FunctionLandscape FunctionLandscape { get; }

            /// <summary>
            /// Returns the current entry.
            /// </summary>
            public FunctionLandscape.Entry Entry { get; }

            /// <summary cref="Transformation.ITransformExecutor.Execute(IRBuilder, TopLevelFunction)"/>
            public bool Execute(IRBuilder builder, TopLevelFunction topLevelFunction) =>
                Parent.PerformTransformation(builder, FunctionLandscape, Entry);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        protected OrderedTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags)
            : base(flags, followUpFlags)
        { }

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        /// <param name="requiresCleanIR">True, iff a previous GC run is required.</param>
        /// <param name="requiresCleanupAfterApplication">True, iff a GC run is required after a successfull application.</param>
        protected OrderedTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags,
            bool requiresCleanIR,
            bool requiresCleanupAfterApplication)
            : base(flags, followUpFlags, requiresCleanIR, requiresCleanupAfterApplication)
        { }

        #endregion

        #region Methods

        /// <summary cref="Transformation.Transform{TPredicate, TManager}(UnsafeFunctionCollection{TPredicate}, TManager)"/>
        public sealed override bool Transform<TPredicate, TManager>(
            UnsafeFunctionCollection<TPredicate> functions,
            TManager transformationManager)
        {
            var landscape = FunctionLandscape.Create<UnsafeFunctionCollection<TPredicate>, TPredicate>(functions);
            if (landscape.Count < 1)
                return false;

            int result = 0;
            using (var irBuilder = functions.CreateBuilder(IRBuilderFlags.PreserveTopLevelFunctions))
            {
                foreach (var entry in landscape)
                {
                    var executor = new Executor(this, landscape, entry);
                    if (ExecuteTransform(irBuilder, entry.Function, transformationManager, executor))
                        ++result;
                }
            }
            return result != 0;
        }

        /// <summary>
        /// Transforms the given top-level function using the provided builder.
        /// </summary>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="landscape">The current function landscape.</param>
        /// <param name="currentEntry">The current top-level function entry.</param>
        protected abstract bool PerformTransformation(
            IRBuilder builder,
            FunctionLandscape landscape,
            FunctionLandscape.Entry currentEntry);

        #endregion
    }
}
