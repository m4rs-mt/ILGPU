// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Optimizer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represetns an optimization level.
    /// </summary>
    public enum OptimizationLevel
    {
        /// <summary>
        /// Lightweight (required) transformations only.
        /// </summary>
        Debug,

        /// <summary>
        /// All transformation passes with seveal optimization
        /// iterations.
        /// </summary>
        Release,
    }

    /// <summary>
    /// Realizes utility helpers to perform and initialize transformations
    /// based on an <see cref="OptimizationLevel"/>.
    /// </summary>
    public static class Optimizer
    {
        /// <summary>
        /// Returns the number of known optimization levels.
        /// </summary>
        public const int NumOptimizationLevels = 2;

        /// <summary>
        /// Populates the given transformation manager with the required
        /// optimization transformations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        /// <param name="level">The desired optimization level.</param>
        /// <returns>The maximum number of iterations.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddOptimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags,
            OptimizationLevel level)
        {
            switch (level)
            {
                case OptimizationLevel.Debug:
                    AddDebugOptimizations(builder, contextFlags);
                    break;
                case OptimizationLevel.Release:
                    AddReleaseOptimizations(builder, contextFlags);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        /// <summary>
        /// Populates the given transformation manager with the required
        /// debug optimization transformations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddDebugOptimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            if (!contextFlags.HasFlags(ContextFlags.NoInlining))
                builder.Add(new Inliner());
            builder.Add(new SimplifyControlFlow());
            builder.Add(new SSAConstruction());
            builder.Add(new DeadCodeElimination());
        }

        /// <summary>
        /// Populates the given transformation manager with the required
        /// release optimization transformations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddReleaseOptimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            if (!contextFlags.HasFlags(ContextFlags.NoInlining))
                builder.Add(new Inliner());
            builder.Add(new SimplifyControlFlow());
            builder.Add(new SSAConstruction());
            builder.Add(new InferAddressSpaces());
            builder.Add(new DeadCodeElimination());
        }

        /// <summary>
        /// Creates a transformer for the given optimization level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="contextFlags">The context flags.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer CreateTransformer(
            this OptimizationLevel level,
            TransformerConfiguration configuration,
            ContextFlags contextFlags)
        {
            var builder = Transformer.CreateBuilder(configuration);
            builder.AddOptimizations(contextFlags, level);
            return builder.ToTransformer();
        }
    }
}
