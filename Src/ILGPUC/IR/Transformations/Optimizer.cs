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

using ILGPU.Runtime;
using ILGPUC.IR.Transformations.KernelTransform;
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
    /// <param name="iterativeInlining">
    /// True if all basic optimizations rely on an iterative inlining strategy that will
    /// inline all possible functions.
    /// </param>
    public static void AddBasicOptimizations(
        this Transformer.Builder builder,
        bool iterativeInlining = true)
    {
        builder.Add(args => new SimplifyControlFlow(args));
        builder.Add(args => new SSAConstruction(args));
        builder.Add(args => new SSACleanup(args));

        if (iterativeInlining)
        {
            // Iterative inliner — runs Inliner + cleanup in a loop until
            // no inline-able calls remain, handling arbitrary call chain
            // depths in a single optimizer slot.
            builder.Add(static args => new IterativeInliner(args));

            // Merge blocks split by SpecializeMethodCall during inlining.
            // The IterativeInliner omits SimplifyControlFlow internally
            // (see comment there), so consecutive inlined call sites leave
            // many single-value blocks that must be merged afterward.
            builder.Add(static args => new SimplifyControlFlow(args));
        }
        else
        {
            // Single inliner pass — used at O0 for minimal inlining of
            // only the smallest methods (trivial getters/wrappers).
            builder.Add(static args => new Inliner(args));
            builder.Add(static args => new SimplifyControlFlow(args));
            builder.Add(static args => new SSAConstruction(args));
            builder.Add(static args => new SSACleanup(args));
        }
    }

    /// <summary>
    /// Adds optimizations passes to convert control-flow ifs into fast predicates.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddConditionalOptimizations(this Transformer.Builder builder)
    {
        builder.Add(static args => new IfConversion(args));
        builder.Add(static args => new SimplifyControlFlow(args));
    }

    /// <summary>
    /// Adds address-space operation optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    /// <remarks>
    /// Converts operations working on the generic address space into operations
    /// working on specific address spaces to improve performance.
    /// </remarks>
    public static void AddAddressSpaceOptimizations(this Transformer.Builder builder) =>
        builder.Add(static args => new InferAddressSpaces(args));

    /// <summary>
    /// Strips debug assertions and IO operations when disabled in compilation
    /// properties. Should be the first pass in the global optimization pipeline
    /// so that subsequent passes operate on clean IR in Release mode.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddDebugSetter(this Transformer.Builder builder) =>
        builder.Add(static args => new DebugSetter(args));

    /// <summary>
    /// Adds general backend optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    /// <param name="specification">The architecture specification.</param>
    public static void AddAcceleratorSpecializer(
        this Transformer.Builder builder,
        ArchitectureSpecification specification)
    {
        // Perform an additional inlining pass to specialize small device-specific
        // functions that could have been introduced
        builder.Add(static args => new Inliner(args));

        // Recognize lambda-based collective operations. At frontend time,
        // lambda Method bodies may not be compiled yet (lazy compilation).
        // Now that the Inliner has run and all bodies are available,
        // convert custom WarpReduce/WarpScan/GroupReduce/GroupScan nodes
        // to intrinsic variants BEFORE DCE can remove them.
        builder.Add(static args => new RecognizeCollectiveOps(args));

        builder.Add(static args => new DeadLoadElimination(args));
        builder.Add(static args => new SimplifyControlFlow(args));

        // Expand Atomic.MakeAtomic<T> call sites (CustomAtomic nodes) into
        // explicit CAS loops before further lowering. Follow with an Inliner
        // pass to inline the operation lambda that LowerCustomAtomic inserts
        // as a MethodCall in the loop body. A later SSAConstruction pass
        // (after ClosureElimination) promotes the loop's currentVar alloca
        // into a proper phi.
        builder.Add(static args => new LowerCustomAtomic(args));
        builder.Add(static args => new Inliner(args));
        builder.Add(static args => new SimplifyControlFlow(args));

        // Specialize accelerator properties, arrays, and views
        builder.Add(static args => new LowerThreadIntrinsics(args));
        builder.Add(static args => new LowerArrays(args));
        if (!specification.SupportsViews)
            builder.Add(static args => new LowerViews(args));

        // Validate and lower GCInit nodes (managed-object markers)
        builder.Add(static args => new LowerGCInit(args));

        // Lower collectives: warp first (expands primitive WarpReduce/WarpScan
        // to shuffles for backends without native support), then group
        // (expands GroupReduce/GroupScan into shared-memory + WarpReduce
        // patterns), then warp AGAIN to lower the newly-created WarpReduce
        // nodes produced by the group lowering step.
        builder.Add(args =>
            new LowerWarpCollectives(args, specification));
        builder.Add(args =>
            new LowerGroupCollectives(args, specification));
        builder.Add(args =>
            new LowerWarpCollectives(args, specification));

        // Eliminate closure allocations by decomposing closure struct Globals
        // into per-field allocas. SSA construction then promotes them to direct
        // SSA values, ensuring no closures survive to backend code generation.
        builder.Add(static args => new ClosureElimination(args));
        builder.Add(static args => new SSAConstruction(args));
        builder.Add(static args => new SSACleanup(args));
        builder.Add(static args => new DeadLoadElimination(args));

        // Normalize Metal/OpenCL kernel entry-point parameters. Flatten view struct
        // params into per-field kernel arguments.
        builder.Add(args =>
            SplitViewLowering.TryCreate(specification.AcceleratorType, args));

        // Thread device constants (Group.Index, Grid.Index, etc.) as explicit
        // parameters through NoInline helper call chains. Required for Metal
        // (MSL has no implicit thread-index builtins in helpers) and CPU
        // (helpers create local laneIdx without the correct base offset).
        if (specification.AcceleratorType is AcceleratorType.Metal
            or AcceleratorType.CPU)
        {
            builder.Add(static args => new MetalKernelLowering(args));
        }

        builder.Add(args =>
            new AcceleratorSpecializer(
                args,
                specification));

        // Perform an second inlining pass to specialize specialized functions
        builder.Add(static args => new SimplifyControlFlow(args));
        builder.Add(static args => new DeadLoadElimination(args));
    }

    /// <summary>
    /// Populates the given transformation manager with O0 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO0Optimizations(this Transformer.Builder builder)
    {
        builder.AddDebugSetter();
        builder.AddBasicOptimizations(iterativeInlining: false);
        builder.AddAddressSpaceOptimizations();
    }

    /// <summary>
    /// Populates the given transformation manager with O1 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO1Optimizations(this Transformer.Builder builder)
    {
        builder.AddDebugSetter();
        builder.AddO1OptimizationsInternal();
    }

    /// <summary>
    /// Populates the given transformation manager with O1 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    private static void AddO1OptimizationsInternal(this Transformer.Builder builder)
    {
        builder.AddBasicOptimizations(iterativeInlining: true);
        builder.AddConditionalOptimizations();
        builder.AddAddressSpaceOptimizations();
    }

    /// <summary>
    /// Populates the given transformation manager with O2 optimizations.
    /// </summary>
    /// <param name="builder">The transformation manager to populate.</param>
    public static void AddO2Optimizations(this Transformer.Builder builder)
    {
        builder.AddO1Optimizations();
        builder.AddO1OptimizationsInternal();
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
