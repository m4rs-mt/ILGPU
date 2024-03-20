// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
        /// <param name="enableAssertions">
        /// Enables use of assertions.
        /// </param>
        /// <param name="forceDebugConfig">
        /// Forces use of debug configuration in O1 and O2 builds.
        /// </param>
        /// <param name="prepareContext">
        /// Prepares the context by setting additional field/initializing specific
        /// items before the accelerator is created.
        /// </param>
        /// <param name="createAccelerator">Creates a new accelerator.</param>
        protected TestContext(
            OptimizationLevel level,
            bool enableAssertions,
            bool forceDebugConfig,
            Action<Context.Builder> prepareContext,
            Func<Context, Accelerator> createAccelerator)
        {
            Context = Context.Create(builder =>
                prepareContext(
                    builder
                    .DebugConfig(
                        enableAssertions: enableAssertions,
                        enableIOOperations: true,
                        debugSymbolsMode: DebugSymbolsMode.Auto,
                        forceDebuggingOfOptimizedKernels: forceDebugConfig,
                        enableIRVerifier: true)
                    .Arrays(ArrayMode.InlineMutableStaticArrays)
                    .Optimize(level)
                    .Profiling()));
            Accelerator = createAccelerator(Context);
        }

        /// <summary>
        /// Returns the current context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated optimization level.
        /// </summary>
        public OptimizationLevel OptimizationLevel =>
            Context.Properties.OptimizationLevel;

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
            Context.ClearCache(ClearCacheMode.Default);
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
