﻿using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Util;
using System;

namespace ILGPU.Tests
{
    /// <summary>
    /// Provides contexts for different configurations.
    /// </summary>
    public abstract class TestContext : DisposeBase
    {
        /// <summary>
        /// Constructs a new context provider.
        /// </summary>
        /// <param name="level">The optimization level.</param>
        /// <param name="prepareContext">
        /// Prepares the context by setting additional field/initializing specific
        /// items before the accelerator is created.
        /// </param>
        /// <param name="createAccelerator">Creates a new accelerator.</param>
        protected TestContext(
            OptimizationLevel level,
            Action<Context> prepareContext,
            Func<Context, Accelerator> createAccelerator)
        {
            Context = new Context(
                ContextFlags.EnableAssertions | ContextFlags.EnableVerifier,
                level);
            prepareContext?.Invoke(Context);
            Accelerator = createAccelerator(Context);
        }

        /// <summary>
        /// Returns the current context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated optimization level.
        /// </summary>
        public OptimizationLevel OptimizationLevel => Context.OptimizationLevel;

        /// <summary>
        /// Returns the current accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        /// <summary>
        /// Ensures a clean test scenario.
        /// </summary>
        public void ClearCaches()
        {
            Accelerator.ClearCache(ClearCacheMode.Everything);
            Context.ClearCache(ClearCacheMode.Everything);
        }

        /// <summary>
        /// Disposes accelerator and context objects.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Accelerator.Dispose();
                Context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
