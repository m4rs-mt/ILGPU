// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;

namespace ILGPU.IR
{
    /// <summary>
    /// Extension methods for context related objects.
    /// </summary>
    public static class IRContextExtensions
    {
        /// <summary>
        /// Gets the main IR context from a main ILGPU context instance.
        /// </summary>
        /// <param name="context">The main ILGPU context instance.</param>
        /// <returns>The main IR context.</returns>
        public static IRContext GetIRContext(this Context context) =>
            context.IRContext;

        /// <summary>
        /// Gets the main IR type context from a main ILGPU context instance.
        /// </summary>
        /// <param name="context">The main ILGPU context instance.</param>
        /// <returns>The main IR type context.</returns>
        public static IRTypeContext GetIRTypeContext(this Context context) =>
            context.TypeContext;

        /// <summary>
        /// Gets the main context transformer from a main ILGPU context instance.
        /// </summary>
        /// <param name="context">The main ILGPU context instance.</param>
        /// <returns>The main context transformer.</returns>
        public static Transformer GetTransformer(this Context context) =>
            context.ContextTransformer;

        /// <summary>
        /// Gets the current intrinsic manager from a main ILGPU context instance.
        /// </summary>
        /// <param name="context">The main ILGPU context instance.</param>
        /// <returns>The current intrinsic manager.</returns>
        public static IntrinsicImplementationManager GetIntrinsicManager(
            this Context context) =>
            context.IntrinsicManager;

        /// <summary>
        /// Gets the current intrinsic implementation manager to register new intrinsics.
        /// </summary>
        /// <param name="builder">The current builder instance.</param>
        /// <returns>The current intrinsic manager.</returns>
        public static IntrinsicImplementationManager GetIntrinsicManager(
            this Context.Builder builder) =>
            builder.IntrinsicManager;
    }
}
