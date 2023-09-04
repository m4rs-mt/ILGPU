// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ParallelCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents a parallel object cache to be used in combination with a
    /// <see cref="Parallel"/> for implementation to avoid unnecessary temporary object
    /// creation.
    /// </summary>
    /// <typeparam name="T">The type of the elements to cache.</typeparam>
    public abstract class ParallelCache<T> : DisposeBase, IParallelCache<T>
        where T : class
    {
        #region Instance

        private InlineList<T> cache;
        private InlineList<T> used;

        /// <summary>
        /// Creates a new parallel cache.
        /// </summary>
        /// <param name="initialCapacity">
        /// The initial number of processing threads (if any).
        /// </param>
        protected ParallelCache(int? initialCapacity = null)
        {
            int capacity = initialCapacity ?? Environment.ProcessorCount * 2;
            cache = InlineList<T>.Create(capacity);
            used = InlineList<T>.Create(capacity);

            LocalInitializer = GetOrCreate;
            LocalFinalizer = FinishProcessing;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying sync root object.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Returns the local initializer function.
        /// </summary>
        public Func<T> LocalInitializer { get; }

        /// <summary>
        /// Returns the local finalizer action.
        /// </summary>
        public Action<T> LocalFinalizer { get; }

        /// <summary>
        /// Returns the underlying used intermediates.
        /// </summary>
        protected ReadOnlySpan<T> Used => used;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes this parallel cache of the next parallel operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeProcessing()
        {
            // This method does not perform an operation at the moment but this may
            // change in the future. For this reason, this (empty) method remains here
            // and should be called in all cases prior to calling GetOrCreate().
        }

        /// <summary>
        /// Gets or creates a new intermediate array tuple storing information for the
        /// upcoming optimizer iteration.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrCreate()
        {
            // Checks the cache contents to retrieve previously
            T intermediate;
            lock (SyncRoot)
            {
                if (cache.Count > 0)
                {
                    int lastIndex = cache.Count - 1;
                    intermediate = cache[lastIndex];
                    cache.RemoveAt(lastIndex);
                }
                else
                {
                    // Create a new intermediate result
                    intermediate = CreateIntermediate();
                }
            }

            // Initialize intermediate result and return
            InitializeIntermediate(intermediate);

            // Add to our list of used intermediates
            lock (SyncRoot)
                used.Add(intermediate);

            return intermediate;
        }

        /// <summary>
        /// Finishes a parallel processing step.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinishProcessing()
        {
            // Return all used intermediates to the cache
            cache.AddRange(used);
            used.Clear();

        }

        /// <summary>
        /// Creates a new intermediate instance without initializing it properly.
        /// </summary>
        /// <returns>The created intermediate state.</returns>
        protected abstract T CreateIntermediate();

        /// <summary>
        /// Initializes the given intermediate state in order to prepare it for
        /// processing.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to prepare.</param>
        protected virtual void InitializeIntermediate(T intermediateState) { }

        /// <summary>
        /// Finishes processing of the current thread while getting an intermediate state.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to operate on.</param>
        protected virtual void FinishProcessing(T intermediateState) { }

        #endregion

        #region IParallelCache

        /// <summary>
        /// Creates a new intermediate instance without initializing it properly.
        /// </summary>
        /// <returns>The created intermediate state.</returns>
        T IParallelCache<T>.CreateIntermediate() => CreateIntermediate();

        /// <summary>
        /// Initializes the given intermediate state in order to prepare it for
        /// processing.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to prepare.</param>
        void IParallelCache<T>.InitializeIntermediate(T intermediateState) =>
            InitializeIntermediate(intermediateState);

        /// <summary>
        /// Finishes processing of the current thread while getting an intermediate state.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to operate on.</param>
        void IParallelCache<T>.FinishProcessing(T intermediateState) =>
            FinishProcessing(intermediateState);

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes all created intermediate states (if required).
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            // Check whether we need to dispose all elements
            if (cache.Count >  0 && typeof(IDisposable).IsAssignableFrom(typeof(T)))
            {
                foreach (var intermediateStates in cache)
                    intermediateStates.AsNotNullCast<IDisposable>().Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// An abstract parallel cache interface operating on intermediate states.
    /// </summary>
    /// <typeparam name="T">The type of all intermediate states.</typeparam>
    public interface IParallelCache<T>
    {
        /// <summary>
        /// Creates a new intermediate instance without initializing it properly.
        /// </summary>
        /// <returns>The created intermediate state.</returns>
        T CreateIntermediate();

        /// <summary>
        /// Initializes the given intermediate state in order to prepare it for
        /// processing.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to prepare.</param>
        void InitializeIntermediate(T intermediateState);

        /// <summary>
        /// Finishes processing of the current thread while getting an intermediate state.
        /// </summary>
        /// <param name="intermediateState">The intermediate state to operate on.</param>
        void FinishProcessing(T intermediateState);
    }

    /// <summary>
    /// An abstract parallel processing body representing a function to be executed
    /// concurrently on a given value range. It operates on intermediate values that are
    /// managed by its surrounding processing cache.
    /// </summary>
    /// <typeparam name="T">The type of all intermediate states.</typeparam>
    public interface IParallelProcessingBody<T>
        where T : class
    {
        /// <summary>
        /// Initializes this processing body to prepare the upcoming parallel processing
        /// steps.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Processes a single element concurrently while accepting an intermediate state
        /// on which this body operates on.
        /// </summary>
        /// <param name="index">The current processing element index.</param>
        /// <param name="loopState">The parallel loop state (if any).</param>
        /// <param name="intermediateState">
        /// The current intermediate state for this thread.
        /// </param>
        void Process(
            int index,
            ParallelLoopState? loopState,
            T intermediateState);

        /// <summary>
        /// Finalizes the current body operating while having the ability to inspect all
        /// previously used intermediate states.
        /// </summary>
        /// <param name="intermediateStates">
        /// A span referring to all previously used intermediate states.
        /// </param>
        void Finalize(ReadOnlySpan<T> intermediateStates);
    }

    /// <summary>
    /// Static helpers for parallel processing extensions.
    /// </summary>
    public static class ParallelProcessing
    {
        /// <summary>
        /// Gets or sets whether debug mode is enabled. Note that this assignment needs to
        /// be changes before the first <see cref="ParallelProcessingCache{T,TBody}"/>
        /// instance has been created since the flag is cached locally to enable JIT
        /// optimizations.
        /// </summary>
        public static bool DebugMode { get; set; }
    }

    /// <summary>
    /// Represents a parallel object cache to be used in combination with a
    /// <see cref="Parallel"/> for implementation to avoid unnecessary temporary object
    /// creation. Furthermore, this implementation operates on specialized body instances
    /// to avoid virtual function calls in each processing step.
    /// </summary>
    /// <typeparam name="T">The type of the elements to cache.</typeparam>
    /// <typeparam name="TBody">The type of the custom loop body instance.</typeparam>
    public abstract class ParallelProcessingCache<T, TBody> : ParallelCache<T>
        where T : class
        where TBody : IParallelProcessingBody<T>
    {
        /// <summary>
        /// Returns true if the debug mode is enabled for all parallel processing
        /// operations.
        /// </summary>
        private static readonly bool DebugMode = ParallelProcessing.DebugMode;

        private readonly Func<int, ParallelLoopState?, T, T> body;
        private readonly TBody bodyImplementation;
        private readonly ParallelOptions defaultOptions = new();

        /// <summary>
        /// Creates a new parallel processing cache operating on intermediate states.
        /// </summary>
        /// <param name="initialCapacity">
        /// The initial number of processing threads (if any).
        /// </param>
        [SuppressMessage(
            "Usage",
            "CA2214:Do not call overridable methods in constructors",
            Justification = "This method is called here as it represents an abstract " +
                "static factory method")]
        protected ParallelProcessingCache(int? initialCapacity = null)
            : base(initialCapacity)
        {
            bodyImplementation = CreateBody();
            body = (i, state, intermediate) =>
            {
                bodyImplementation.Process(i, state, intermediate);
                return intermediate;
            };
        }

        /// <summary>
        /// Creates the required parallel processing body to be used.
        /// </summary>
        /// <returns>The processing body to use.</returns>
        protected abstract TBody CreateBody();

        /// <summary>
        /// Performs the current operation in parallel.
        /// </summary>
        /// <param name="fromInclusive">The inclusive start index.</param>
        /// <param name="toExclusive">The exclusive end index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParallelFor(int fromInclusive, int toExclusive) =>
            ParallelFor(fromInclusive, toExclusive, defaultOptions);

        /// <summary>
        /// Performs the current operation in parallel.
        /// </summary>
        /// <param name="fromInclusive">The inclusive start index.</param>
        /// <param name="toExclusive">The exclusive end index.</param>
        /// <param name="options">The parallel execution options.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParallelFor(
            int fromInclusive,
            int toExclusive,
            ParallelOptions options)
        {
            // Initialize processing cache
            InitializeProcessing();

            // Initialize operation
            bodyImplementation.Initialize();

            // Check for enabled debug mode
            if (DebugMode)
            {
                var intermediate = GetOrCreate();
                for (int i = fromInclusive; i < toExclusive; ++i)
                    body(i, null, intermediate);
            }
            else
            {
                Parallel.For(
                    fromInclusive,
                    toExclusive,
                    options,
                    LocalInitializer,
                    body,
                    LocalFinalizer);
            }

            // Finalize operation
            bodyImplementation.Finalize(Used);
            FinishProcessing();
        }
    }
}
