// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Optimizer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Backends.PointerViews;
using System;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Realizes utility helpers to perform and initialize transformations
/// based on an <see cref="OptimizationLevel"/>.
/// </summary>
static class Optimizer
{
    /// <summary>
    /// Returns the number of known optimization levels.
    /// </summary>
    public const int NumOptimizationLevels = 3;

    /// <summary>
    /// Internal mapping from optimization levels to handlers.
    /// </summary>
    private static readonly Action<Transformer.Builder>[]
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
    /// <param name="level">The desired optimization level.</param>
    /// <returns>The maximum number of iterations.</returns>
    public static void AddOptimizations(
        this Transformer.Builder builder,
        OptimizationLevel level)
    {
        if (level < OptimizationLevel.O0 || level > OptimizationLevel.O2)
            throw new ArgumentOutOfRangeException(nameof(level));
        OptimizationHandlers[(int)level](builder);
    }

    /// <summary>
    /// Adds basic optimization transformations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddBasicOptimizations(this Transformer.Builder builder)
    {
        builder.Add(new SSAConstruction());
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
    /// <param name="backend">The current backend instance.</param>
    /// <param name="context">The kernel backend context to compile.</param>
    public static void AddAcceleratorSpecializer(
        this Transformer.Builder builder,
        Backend backend,
        IRContext context)
    {
        // Perform an additional inlining pass to specialize small device-specific
        // functions that could have been introduced
        builder.Add(new Inliner());

        // Specialize accelerator properties, arrays and views
        builder.Add(new LowerArrays(MemoryAddressSpace.Local));
        builder.Add(new LowerPointerViews());
        builder.Add(
            new AcceleratorSpecializer(
                backend.AcceleratorType,
                backend.CurrentWarpSize,
                context.PointerType,
                context.Properties.EnableAssertions,
                context.Properties.EnableIOOperations));

        // Perform an second inlining pass to specialize specialized functions
        builder.Add(new Inliner());

        // Apply UCE and DCE passes to avoid dead branches and fold conditionals that
        // do not affect the actual code being executed
        builder.Add(new UnreachableCodeElimination());
        builder.Add(new DeadCodeElimination());
    }

    /// <summary>
    /// Adds general backend optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    /// <param name="level">The desired optimization level.</param>
    public static void AddBackendOptimizations<TPlacementStrategy>(
        this Transformer.Builder builder,
        OptimizationLevel level)
        where TPlacementStrategy : struct, CodePlacement.IPlacementStrategy
    {
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
        // LowerArrays, LowerPointerViews, AcceleratorSpecializer and
        // AddressSpaceSpecializer
        builder.Add(new LowerStructures());

        // Apply UCE and DCE phases in release mode to remove all dead values and
        // branches that could be have been created in prior passes
        builder.Add(new UnreachableCodeElimination());
        builder.Add(new DeadCodeElimination());

        // Converts local memory arrays into compile-time known structures
        builder.Add(new SSAStructureConstruction());
        builder.Add(new DeadCodeElimination());

        // Infer all specialized address spaces
        if (level > OptimizationLevel.O1)
            builder.Add(new InferLocalAddressSpaces());
        else
            builder.Add(new InferAddressSpaces());

        // Final cleanup phases to improve performance
        builder.Add(new CleanupBlocks());
        builder.Add(new SimplifyControlFlow());

        if (level > OptimizationLevel.O1)
        {
            // Add additional code placement optimizations to reduce register
            // pressure and improve performance
            builder.AddLoopOptimizations();
            builder.Add(new DeadCodeElimination());
            builder.Add(new CodePlacement<TPlacementStrategy>(
                CodePlacementMode.Aggressive));
        }
    }

    /// <summary>
    /// Populates the given transformation manager with O0 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO0Optimizations(this Transformer.Builder builder)
    {
        builder.AddBasicOptimizations();
        builder.AddAddressSpaceOptimizations();
    }

    /// <summary>
    /// Populates the given transformation manager with O1 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO1Optimizations(this Transformer.Builder builder)
    {
        builder.AddBasicOptimizations();
        builder.AddStructureOptimizations();
        builder.AddLoopOptimizations();
        builder.AddConditionalOptimizations();
        builder.AddAddressSpaceOptimizations();
    }

    /// <summary>
    /// Populates the given transformation manager with O2 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO2Optimizations(this Transformer.Builder builder)
    {
        builder.AddBasicOptimizations();
        builder.AddStructureOptimizations();
        builder.AddLoopOptimizations();

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
    /// <returns>The created transformer.</returns>
    public static Transformer CreateTransformer(this OptimizationLevel level)
    {
        var builder = Transformer.CreateBuilder();
        builder.AddOptimizations(level);
        return builder.ToTransformer();
    }
}
