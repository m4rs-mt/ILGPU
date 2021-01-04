// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Optimizer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        private static readonly Action<Transformer.Builder, ContextFlags>[]
            OptimizationHandlers =
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
            builder.Add(new SSAConstruction());
            builder.Add(new DeadCodeElimination());
        }

        /// <summary>
        /// Adds structure optimization passes that lower and remove structure values.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <remarks>
        /// Helps to reduce register pressure and to avoid unnecessary allocations.
        /// </remarks>
        public static void AddStructureOptimizations(
            this Transformer.Builder builder)
        {
            builder.Add(new LowerStructures());
            builder.Add(new DeadCodeElimination());
        }

        /// <summary>
        /// Adds loop-specific optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <remarks>
        /// Loop-invariant code will be moved out of loops, loops with a known trip
        /// count will be unrolled and (potentially new) unreachable code will be
        /// removed.
        /// </remarks>
        public static void AddLoopOptimizations(
            this Transformer.Builder builder)
        {
            builder.Add(new LoopInvariantCodeMotion());
            builder.Add(new LoopUnrolling());
            builder.Add(new UnreachableCodeElimination());
            builder.Add(new DeadCodeElimination());
            builder.Add(new SimplifyControlFlow());
        }

        /// <summary>
        /// Adds optimizations passes to convert control-flow ifs into fast predicates.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        public static void AddConditionalOptimizations(
            this Transformer.Builder builder)
        {
            builder.Add(new IfConversion());
            builder.Add(new SimplifyControlFlow());
        }

        /// <summary>
        /// Adds address-space operation optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <remarks>
        /// Converts operations working on the generic address space into operations
        /// working on specific address spaces to improve performance.
        /// </remarks>
        public static void AddAddressSpaceOptimizations(
            this Transformer.Builder builder) =>
            builder.Add(new InferAddressSpaces());

        /// <summary>
        /// Adds general backend optimizations.
        /// </summary>
        /// <param name="builder">The transformation manager to populate.</param>
        /// <param name="acceleratorSpecializer">
        /// An instance of an <see cref="AcceleratorSpecializer"/> class.
        /// </param>
        /// <param name="contextFlags">The context flags.</param>
        /// <param name="level">The desired optimization level.</param>
        public static void AddBackendOptimizations(
            this Transformer.Builder builder,
            AcceleratorSpecializer acceleratorSpecializer,
            ContextFlags contextFlags,
            OptimizationLevel level)
        {
            // Specialize accelerator properties and views
            builder.Add(new LowerPointerViews());
            builder.Add(acceleratorSpecializer);

            // Perform an additional inlining pass to specialize small device-specific
            // functions that could have been introduced
            if (!contextFlags.HasFlags(ContextFlags.NoInlining))
                builder.Add(new Inliner());

            // Skip further optimizations in debug mode
            if (level < OptimizationLevel.O1)
                return;

            // Use experimental address-space specializer in O2 only
            if (level > OptimizationLevel.O1)
            {
                // Specialize all parameter address spaces
                builder.Add(new InferKernelAddressSpaces(MemoryAddressSpace.Global));
            }

            // Lower all value structures that could have been created during the
            // following passes:
            // LowerPointerViews, AcceleratorSpecializer and AddressSpaceSpecializer
            builder.Add(new LowerStructures());

            // Apply DCE phase in release mode to remove all dead values that
            // could be created in prior passes
            builder.Add(new DeadCodeElimination());

            // Infer all specialized address spaces
            if (level > OptimizationLevel.O1)
                builder.Add(new InferLocalAddressSpaces());
            else
                builder.Add(new InferAddressSpaces());

            // Final cleanup phases to improve performance
            builder.Add(new CleanupBlocks());
            builder.Add(new SimplifyControlFlow());
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
            builder.AddAddressSpaceOptimizations();
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
            builder.AddStructureOptimizations();
            builder.AddLoopOptimizations();
            builder.AddConditionalOptimizations();
            builder.AddAddressSpaceOptimizations();
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
            builder.AddStructureOptimizations();
            builder.AddLoopOptimizations();

            // Converts local memory arrays into structure values
            builder.Add(new SSAStructureConstruction());
            // Append experimental if-condition conversion pass
            builder.Add(new IfConditionConversion());
            // Remove all temporarily generated values that are no longer required
            builder.Add(new DeadCodeElimination());

            builder.AddConditionalOptimizations();
            builder.AddAddressSpaceOptimizations();
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
