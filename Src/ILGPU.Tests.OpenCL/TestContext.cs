// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.OpenCL;
using System;

namespace ILGPU.Tests.OpenCL
{
    /// <summary>
    /// An abstract test context for OpenCL accelerators.
    /// </summary>
    public abstract class CLTestContext : TestContext
    {
        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="enableAssertions">
        /// Enables use of assertions.
        /// </param>
        /// <param name="forceDebugConfig">
        /// Forces use of debug configuration in O1 and O2 builds.
        /// </param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CLTestContext(
            OptimizationLevel optimizationLevel,
            bool enableAssertions,
            bool forceDebugConfig,
            Action<Context.Builder> prepareContext)
            : base(
                  optimizationLevel,
                  enableAssertions,
                  forceDebugConfig,
                  builder => prepareContext(builder.OpenCL()),
                  context => context.CreateCLAccelerator(0))
        { }
    }
}
