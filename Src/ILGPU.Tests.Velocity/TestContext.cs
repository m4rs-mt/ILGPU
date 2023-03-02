// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Velocity;
using System;

namespace ILGPU.Tests.Velocity
{
    /// <summary>
    /// An abstract test context for Velocity accelerators.
    /// </summary>
    public abstract class VelocityTestContext : TestContext
    {
        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        /// <param name="prepareContext">The context preparation handler.</param>
        protected VelocityTestContext(
            OptimizationLevel optimizationLevel,
            Action<Context.Builder> prepareContext)
            : base(
                  optimizationLevel,
                  builder => prepareContext(builder.Velocity()),
                  context => context.CreateVelocityAccelerator())
        { }

        /// <summary>
        /// Creates a new test context instance.
        /// </summary>
        /// <param name="optimizationLevel">The optimization level to use.</param>
        protected VelocityTestContext(OptimizationLevel optimizationLevel)
            : this(optimizationLevel, _ => { })
        { }
    }
}
