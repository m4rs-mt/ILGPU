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

using ILGPU.Backends.PointerViews;
using System;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represent an optimization level.
    /// </summary>
    public enum OptimizationLevel
    {
        /// <summary>
        /// Defaults to O0.
        /// </summary>
        Debug = O0,

        /// <summary>
        /// Defaults to O1.
        /// </summary>
        Release = O1,

        /// <summary>
        /// Lightweight (required) transformations only.
        /// </summary>
        O0 = 0,

        /// <summary>
        /// Default release mode transformations.
        /// </summary>
        O1 = 1,

        /// <summary>
        /// Expensive transformations.
        /// </summary>
        O2 = 2,
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
        public const int NumOptimizationLevels = 3;

        /// <summary>
        /// Internal mapping from optimization levels to handlers.
        /// </summary>
        private static readonly Action<Transformer.Builder, ContextFlags>[] OptimizationHandlers =
        {
            AddO0Optimizations,
            AddO1Optimizations,
            AddO2Optimizations,
        };

        /// <summary>
        /// Populates the given transformation manager with the required
        /// optimization transformations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        /// <param name="level">The desired optimization level.</param>
        /// <returns>The maximum number of iterations.</returns>
        public static void AddOptimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags,
            OptimizationLevel level)
        {
            if (level < OptimizationLevel.O0 || level > OptimizationLevel.O2)
                throw new ArgumentOutOfRangeException(nameof(level));
            OptimizationHandlers[(int)level](builder, contextFlags);
        }

        /// <summary>
        /// Adds basic optimization transformations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddBasicOptimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            if (!contextFlags.HasFlags(ContextFlags.NoInlining))
                builder.Add(new Inliner());
            builder.Add(new SimplifyControlFlow());
        }

        /// <summary>
        /// Adds general backend optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="acceleratorSpecializer">
        /// An instance of an <see cref="AcceleratorSpecializer"/> class.
        /// </param>
        /// <param name="level">The desired optimization level.</param>
        public static void AddBackendOptimizations(
            this Transformer.Builder builder,
            AcceleratorSpecializer acceleratorSpecializer,
            OptimizationLevel level)
        {
            // Specialize accelerator properties and views
            builder.Add(new LowerArrays());
            builder.Add(new LowerPointerViews());
            builder.Add(acceleratorSpecializer);

            // Lower structures
            if (level > OptimizationLevel.O1)
            {
                builder.Add(new LowerStructures());
                builder.Add(new DeadCodeElimination());
            }
        }

        /// <summary>
        /// Populates the given transformation manager with O0 optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddO0Optimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            builder.AddBasicOptimizations(contextFlags);
            builder.Add(new SSAConstruction());
            builder.Add(new DeadCodeElimination());
            builder.Add(new InferAddressSpaces());
        }

        /// <summary>
        /// Populates the given transformation manager with O1 optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddO1Optimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            builder.AddBasicOptimizations(contextFlags);
            builder.Add(new DeadCodeElimination());
            builder.Add(new SSAConstruction());
            builder.Add(new DeadCodeElimination());
            builder.Add(new InferAddressSpaces());
        }

        /// <summary>
        /// Populates the given transformation manager with O2 optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="contextFlags">The context flags.</param>
        public static void AddO2Optimizations(
            this Transformer.Builder builder,
            ContextFlags contextFlags)
        {
            builder.AddBasicOptimizations(contextFlags);
            builder.Add(new DeadCodeElimination());
            builder.Add(new SSAConstruction());
            builder.Add(new LowerStructures());
            builder.Add(new DeadCodeElimination());
            builder.Add(new InferAddressSpaces());
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
