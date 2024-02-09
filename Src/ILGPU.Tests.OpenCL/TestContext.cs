﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
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
        /// <param name="prepareContext">The context preparation handler.</param>
        protected CLTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context.Builder> prepareContext)
            : base(
                  optimizationLevel,
                  builder => prepareContext(builder.OpenCL()),
                  context => context.CreateCLAccelerator(0))
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        protected CLTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, _ => { })
        { }
    }
}
