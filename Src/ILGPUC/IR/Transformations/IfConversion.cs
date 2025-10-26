// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PostDominators = ILGPUC.IR.Analyses.Dominators<
    ILGPUC.IR.MethodValues.Backwards>;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Converts nested if/switch branches into value conditionals (predicated execution).
/// </summary>
/// <remarks>
/// This transformation identifies simple if/else branches where both paths have no side
/// effects and are small enough, then converts them to predicated instructions using
/// conditional values (select/predicate operations). This enables better instruction
/// level parallelism and reduces branch mis-prediction costs.
/// </remarks>
/// <param name="args">The transformation arguments.</param>
/// <param name="maxBlockSize">
/// The maximum total number of instructions in all branches combined.
/// </param>
/// <param name="maxBlockDifference">
/// The maximum difference in instruction count between branches.
/// </param>
sealed class IfConversion(
    TransformationArgs args,
    int maxBlockSize = 4,
    int maxBlockDifference = 4) :
    Transformation<ValueMap<Method, BasicBlock, IfConversion.ConversionInfo>>(args)
{
    /// <summary>
    /// Information about a convertible branch.
    /// </summary>
    /// <param name="BranchBlock">The block containing the conditional branch.</param>
    /// <param name="PostDominator">The common post-dominator.</param>
    /// <param name="TrueRegion">Blocks in the true branch.</param>
    /// <param name="FalseRegion">Blocks in the false branch.</param>
    /// <param name="PhisToConvert">Phi values that need conversion.</param>
    internal readonly record struct ConversionInfo(
        BasicBlock BranchBlock,
        BasicBlock PostDominator,
        HashSet<BasicBlock> TrueRegion,
        HashSet<BasicBlock> FalseRegion,
        InlineList<PhiValue> PhisToConvert);

    /// <summary>
    /// Analyzes the given method to find convertible branches.
    /// </summary>
    protected override ValueMap<Method, BasicBlock, ConversionInfo> CreateIntermediate(
        ModuleTransform transform,
        Method method)
    {
        var postDominators = method.Blocks.CreatePostDominators();
        var result = method.CreateMap<BasicBlock, ConversionInfo>();
        foreach (var block in method.Blocks)
        {
            var analyzed = TryAnalyzeBranch(block, postDominators);
            if (analyzed.HasValue)
                result.Add(block, analyzed.Value);
        }
        return result;
    }

    /// <summary>
    /// Attempts to analyze a branch for conversion.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ConversionInfo? TryAnalyzeBranch(
        BasicBlock block,
        PostDominators postDominators)
    {
        // Only handle two-way conditional branches
        if (block.TerminationKind != BlockTerminationKind.Conditional &&
            block.TerminationKind != BlockTerminationKind.Switch)
            return null;

        // Must have exactly 2 successors
        var successors = block.Successors;
        if (successors.Length != 2)
            return null;

        // Find common post-dominator
        var postDominator = postDominators.GetImmediateCommonDominator(successors);
        if (postDominator is null)
            return null;

        // Gather and validate both regions
        var gathered = new HashSet<BasicBlock> { block };
        if (!GatherRegion(successors[0], postDominator, gathered,
                out var trueRegion, out int trueSize) ||
            !GatherRegion(successors[1], postDominator, gathered,
                out var falseRegion, out int falseSize))
        {
            return null;
        }

        // Check size constraints
        int maxRegionSize = Math.Max(trueSize, falseSize);
        int sizeDifference = Math.Abs(trueSize - falseSize);
        if (maxRegionSize > maxBlockSize || sizeDifference > maxBlockDifference)
            return null;

        // Verify predecessors are properly structured
        if (!VerifyPredecessors(block, trueRegion) ||
            !VerifyPredecessors(block, falseRegion))
        {
            return null;
        }

        // Find phi values that need to be converted
        var phisToConvert = FindPhisToConvert(
            postDominator,
            gathered,
            trueRegion,
            falseRegion);
        if (phisToConvert is null)
            return null;

        return new ConversionInfo(
            block,
            postDominator,
            trueRegion,
            falseRegion,
            phisToConvert.Value);
    }

    /// <summary>
    /// Gathers all blocks in a region between start and the post-dominator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool GatherRegion(
        BasicBlock start,
        BasicBlock postDominator,
        HashSet<BasicBlock> gathered,
        out HashSet<BasicBlock> region,
        out int size)
    {
        region = [];
        size = 0;

        var stack = new Stack<BasicBlock>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            // Reached the exit
            if (current == postDominator)
                continue;

            // Already visited in this region
            if (!region.Add(current))
                continue;

            // Block shared between regions (invalid)
            if (!gathered.Add(current))
                return false;

            // Reject blocks with side effects
            if (HasSideEffects(current))
                return false;

            size += current.Count;

            // Continue with successors
            foreach (var successor in current.Successors)
                stack.Push(successor);
        }

        // Handle degenerate case where region is empty (critical edge)
        if (region.Count == 0)
        {
            region.Add(start);
            size = start.Count;
        }

        return true;
    }

    /// <summary>
    /// Checks if a block has side effects.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSideEffects(BasicBlock block)
    {
        foreach (var value in block.Values)
        {
            if (value is MemoryValue or MethodCall)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Verifies that all predecessors of blocks in the region are valid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool VerifyPredecessors(BasicBlock root, HashSet<BasicBlock> region)
    {
        foreach (var block in region)
        {
            foreach (var predecessor in block.Predecessors)
            {
                if (predecessor != root && !region.Contains(predecessor))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Finds phi values in the post-dominator that need to be converted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static InlineList<PhiValue>? FindPhisToConvert(
        BasicBlock postDominator,
        HashSet<BasicBlock> allGathered,
        HashSet<BasicBlock> trueRegion,
        HashSet<BasicBlock> falseRegion)
    {
        var result = InlineList<PhiValue>.Create(2);

        // Find all phi values in the post-dominator that reference our regions.
        // Note: block.Values excludes phi values (ValueCollectionEnumerator starts at
        // FirstValue, not FirstPhiValue). Use PhiValues instead.
        foreach (PhiValue phi in postDominator.PhiValues)
        {
            // Check if this phi references blocks from our regions
            bool referencesRegions = false;
            foreach (var source in phi.Sources)
            {
                if (allGathered.Contains(source))
                {
                    referencesRegions = true;
                    break;
                }
            }

            if (!referencesRegions)
                continue;

            // Phi must have exactly 2 sources (one from each region)
            if (phi.NumArguments != 2)
                return null;

            // Find which source belongs to which region
            Value? trueValue = null;
            Value? falseValue = null;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                var sourceBlock = phi.Sources[i];

                if (trueRegion.Contains(sourceBlock))
                {
                    if (trueValue is not null)
                        return null; // Multiple sources from true region
                    trueValue = phi.GetValue<Value>(i);
                }
                else if (falseRegion.Contains(sourceBlock))
                {
                    if (falseValue is not null)
                        return null; // Multiple sources from false region
                    falseValue = phi.GetValue<Value>(i);
                }
                else
                {
                    return null; // Source from outside our regions
                }
            }

            if (trueValue is null || falseValue is null)
                return null;

            result.Add(phi);
        }

        return result;
    }

    /// <summary>
    /// Converts a phi value to a predicate if it's in a convertible branch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? ConvertPhi(BasicBlockTransform blockTransform, PhiValue phi)
    {
        // Check if this phi is in a post-dominator of a convertible branch
        foreach (var entry in GetIntermediate(blockTransform))
        {
            var info = entry.Value;
            if (phi.BasicBlock != info.PostDominator)
                continue;

            if (!info.PhisToConvert.AsReadOnlySpan().Contains(phi))
                continue;

            // Find true and false values
            Value? trueValue = null;
            Value? falseValue = null;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                var source = phi.Sources[i];
                var value = phi.GetValue<Value>(i);

                if (info.TrueRegion.Contains(source))
                    trueValue = value;
                else if (info.FalseRegion.Contains(source))
                    falseValue = value;
            }

            if (trueValue is null || falseValue is null)
                return phi;

            // Create predicate operation
            var branchCondition = info.BranchBlock.TerminationCondition;
            if (branchCondition is null)
                return phi;

            var condition = blockTransform.Rewrite(branchCondition);
            var mappedTrue = blockTransform.Rewrite(trueValue);
            var mappedFalse = blockTransform.Rewrite(falseValue);

            if (condition is null || mappedTrue is null || mappedFalse is null)
                return phi;

            return blockTransform.CreatePredicate(
                phi.Location,
                condition,
                mappedTrue,
                mappedFalse);
        }

        return phi;
    }

    /// <summary>
    /// Performs the transformation on the method.
    /// </summary>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        // Invoke ConvertPhi for phi values in each block.
        // base.OnTransform iterates block.Values (ValueCollectionEnumerator), which
        // starts at FirstValue (non-phi) and therefore excludes phi values entirely.
        // We must iterate phi values explicitly here.
        foreach (var block in transform.OldMethod.Blocks)
        {
            if (transform.TryGetReplaced(block, out var newBlock) &&
                transform.GetBasicBlockTransform(block).BasicBlock != newBlock)
            {
                continue;
            }

            var blockTransform = transform.GetBasicBlockTransform(block);
            foreach (PhiValue phi in block.PhiValues)
            {
                var result = ConvertPhi(blockTransform, phi);
                if (result is not null && result != phi)
                    transform.Replace(phi, result);
            }
        }

        // Convert conditional/switch branches to unconditional in convertible patterns
        var intermediate = GetIntermediate(transform);
        foreach (var (branchBlock, info) in intermediate)
        {
            // Get the transform for the branch block
            var blockTransform = transform.GetBasicBlockTransform(branchBlock);

            // Convert to unconditional branch to the post-dominator
            blockTransform.CreateUnconditionalTermination(
                blockTransform.RewriteAs<BasicBlock>(info.PostDominator));
        }
    }
}

/// <summary>
/// Transforms and-also and or-else branch chains into efficient logical operations.
/// </summary>
/// <remarks>
/// This transformation identifies chains of conditional branches connected by simple
/// boolean logic (e.g., "if (a) if (b) if (c) then X else Y") and converts them into
/// compound boolean expressions. This reduces the number of basic blocks and enables
/// better optimization opportunities.
/// </remarks>
/// <param name="args">The transformation arguments.</param>
/// <param name="maxBlockSize">
/// The maximum number of instructions in an inner block.
/// </param>
sealed class IfConditionConversion(TransformationArgs args, int maxBlockSize = 4) :
    Transformation<ValueMap<Method, BasicBlock, IfConditionConversion.ChainInfo>>(args)
{
    /// <summary>
    /// Information about a convertible if-chain.
    /// </summary>
    /// <param name="EntryBlock">The entry block of the chain.</param>
    /// <param name="InnerBlocks">All inner blocks in the chain.</param>
    /// <param name="TrueExit">The true exit block.</param>
    /// <param name="FalseExit">The false exit block.</param>
    /// <param name="PhisToRemap">Phi values that need remapping.</param>
    internal readonly record struct ChainInfo(
        BasicBlock EntryBlock,
        HashSet<BasicBlock> InnerBlocks,
        BasicBlock TrueExit,
        BasicBlock FalseExit,
        InlineList<PhiValue> PhisToRemap);

    /// <summary>
    /// Analyzes the given method to find convertible branches.
    /// </summary>
    protected override ValueMap<Method, BasicBlock, ChainInfo> CreateIntermediate(
        ModuleTransform transform,
        Method method)
    {
        var blocks = method.Blocks;
        var dominators = blocks.CreateDominators();
        var phiSources = blocks.ComputePhiSources();

        // Process in post order to find chains from leaves upward
        var result = method.CreateMap<BasicBlock, ChainInfo>();
        foreach (var block in blocks.AsOrder<PostOrder<BasicBlock>>())
        {
            var info = TryAnalyzeChain(block, dominators, phiSources, blocks.Count);
            if (info.HasValue)
                result.Add(block, info.Value);
        }
        return result;
    }

    /// <summary>
    /// Attempts to analyze a block as the start of an if-chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ChainInfo? TryAnalyzeChain(
        BasicBlock entryBlock,
        in Dominators<Forwards> dominators,
        ValueSet<Method, BasicBlock> phiSources,
        int maxBlocks)
    {
        // Must start with an if-branch
        if (entryBlock.TerminationKind != BlockTerminationKind.Conditional)
            return null;

        // Traverse to find all blocks in the chain
        var innerBlocks = new HashSet<BasicBlock>();
        var exitBlocks = new HashSet<BasicBlock>();
        if (!TraverseChain(
            entryBlock,
            dominators,
            phiSources,
            innerBlocks,
            exitBlocks,
            maxBlocks))
        {
            return null;
        }

        // Need at least 4 blocks and exactly 2 exits
        if (innerBlocks.Count < 3 || exitBlocks.Count != 2)
            return null;

        // Find the true and false exit blocks
        var trueExit = FindTrueExit(entryBlock, innerBlocks, exitBlocks);
        if (trueExit is null)
            return null;

        var falseExit = exitBlocks.First(b => b != trueExit);

        // Check for local phis in inner blocks (not allowed)
        if (HasLocalPhis(innerBlocks))
            return null;

        // Find phis that need remapping
        var phisToRemap = FindPhisToRemap(innerBlocks, exitBlocks, trueExit);
        if (phisToRemap is null)
            return null;

        return new ChainInfo(
            entryBlock,
            innerBlocks,
            trueExit,
            falseExit,
            phisToRemap.Value);
    }

    /// <summary>
    /// Traverses the control flow to find all blocks in the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TraverseChain(
        BasicBlock entryBlock,
        in Dominators<Forwards> dominators,
        ValueSet<Method, BasicBlock> phiSources,
        HashSet<BasicBlock> innerBlocks,
        HashSet<BasicBlock> exitBlocks,
        int maxBlocks)
    {
        var queue = new Queue<BasicBlock>(4);
        queue.Enqueue(entryBlock);
        innerBlocks.Add(entryBlock);

        int exitCount = 0;

        while (queue.Count > 0)
        {
            if (innerBlocks.Count + exitBlocks.Count > maxBlocks)
                return false;

            var current = queue.Dequeue();

            if (IsConvertibleBlock(current, entryBlock, dominators, phiSources))
            {
                // This is an inner block - process successors
                foreach (var successor in current.Successors)
                {
                    if (!innerBlocks.Contains(successor) &&
                        !exitBlocks.Contains(successor))
                    {
                        innerBlocks.Add(successor);
                        queue.Enqueue(successor);
                    }
                }
            }
            else
            {
                // This is an exit block
                exitBlocks.Add(current);
                exitCount++;
            }
        }

        return exitCount == 2;
    }

    /// <summary>
    /// Checks if a block can be converted as part of the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsConvertibleBlock(
        BasicBlock block,
        BasicBlock entryBlock,
        in Dominators<Forwards> dominators,
        ValueSet<Method, BasicBlock> phiSources)
    {
        return block.TerminationKind == BlockTerminationKind.Conditional &&
               dominators.Dominates(entryBlock, block) &&
               block.Count <= maxBlockSize &&
               !HasSideEffects(block) &&
               !phiSources.Contains(block);
    }

    /// <summary>
    /// Checks if a block has side effects.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSideEffects(BasicBlock block)
    {
        foreach (var value in block.Values)
        {
            if (value is MemoryValue or MethodCall)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds the true exit block by following true branches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static BasicBlock? FindTrueExit(
        BasicBlock entryBlock,
        HashSet<BasicBlock> innerBlocks,
        HashSet<BasicBlock> exitBlocks)
    {
        var current = entryBlock;
        var visited = new HashSet<BasicBlock>();

        while (innerBlocks.Contains(current) && visited.Add(current))
        {
            if (!current.TryGetConditionalView(out var conditionalValue))
                break;

            current = conditionalValue.TrueTarget;
            if (exitBlocks.Contains(current))
                return current;
        }

        return null;
    }

    /// <summary>
    /// Checks if there are any local phi values in inner blocks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasLocalPhis(HashSet<BasicBlock> innerBlocks)
    {
        // block.Values excludes phi values (see ValueCollectionEnumerator);
        // check NumPhiValues directly instead.
        foreach (var block in innerBlocks)
        {
            if (block.NumPhiValues > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds phi values in exit blocks that reference the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static InlineList<PhiValue>? FindPhisToRemap(
        HashSet<BasicBlock> innerBlocks,
        HashSet<BasicBlock> exitBlocks,
        BasicBlock trueExit)
    {
        var result = InlineList<PhiValue>.Create(2);

        foreach (var exitBlock in exitBlocks)
        {
            // exitBlock.Values excludes phi values; use PhiValues instead.
            foreach (PhiValue phi in exitBlock.PhiValues)
            {
                // Check if this phi references blocks from the chain
                bool hasChainSource = false;
                foreach (var source in phi.Sources)
                {
                    if (innerBlocks.Contains(source))
                    {
                        hasChainSource = true;
                        break;
                    }
                }

                if (!hasChainSource)
                    continue;

                // Verify all inner sources have the same value
                Value? trueValue = null;
                Value? falseValue = null;
                bool isTrueExit = exitBlock == trueExit;

                for (int i = 0; i < phi.NumArguments; i++)
                {
                    var source = phi.Sources[i];
                    if (!innerBlocks.Contains(source))
                        continue;

                    var phiValue = phi.GetValue<Value>(i);
                    if (isTrueExit)
                    {
                        if (trueValue is not null && trueValue != phiValue)
                            return null;
                        trueValue = phiValue;
                    }
                    else
                    {
                        if (falseValue is not null && falseValue != phiValue)
                            return null;
                        falseValue = phiValue;
                    }
                }

                result.Add(phi);
            }
        }

        return result;
    }

    /// <summary>
    /// Performs the transformation by merging chains and creating compound conditions.
    /// </summary>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        // Transform all identified chains
        foreach (var (entryBlock, chainInfo) in GetIntermediate(transform))
            TransformChain(transform, entryBlock, chainInfo);
    }

    /// <summary>
    /// Transforms a single if-chain into a compound boolean expression.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void TransformChain(
        MethodTransform transform,
        BasicBlock entryBlock,
        ChainInfo chainInfo)
    {
        // Get the entry block transform
        var entryTransform = transform.GetBasicBlockTransform(entryBlock);

        // Move all values from inner blocks to the entry block
        var last = entryBlock.LastValue;
        foreach (var block in chainInfo.InnerBlocks)
        {
            if (block == entryBlock)
                continue;

            if (block.FirstValue is not null)
            {
                transform.AppendTo(last, block.FirstValue);
                last = block.LastValue;
            }
        }

        // Build the compound condition recursively
        if (!entryBlock.TryGetConditionalView(out var conditionalView))
            return;

        var condition = BuildCompoundCondition(
            entryTransform,
            entryBlock,
            conditionalView,
            chainInfo.InnerBlocks,
            chainInfo.TrueExit,
            chainInfo.FalseExit);

        if (condition is null)
            return;

        // Set new conditional termination with compound condition
        entryTransform.CreateConditionalTermination(
            condition,
            entryTransform.RewriteAs<BasicBlock>(chainInfo.TrueExit),
            entryTransform.RewriteAs<BasicBlock>(chainInfo.FalseExit));

        // Remap phi values - consolidate all inner block sources to entry block
        // Since we verified in FindPhisToRemap that all inner sources have the same value,
        // we can replace the phi with a simpler version that only has the entry block as source
        RemapPhiValues(transform, chainInfo);
    }

    /// <summary>
    /// Remaps phi values to consolidate inner block sources into the entry block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void RemapPhiValues(
        MethodTransform transform,
        ChainInfo chainInfo)
    {
        foreach (var phi in chainInfo.PhisToRemap)
        {
            // Find the value from inner blocks and external sources
            Value? innerValue = null;
            var externalSources = InlineList<(BasicBlock, Value)>.Create(phi.NumArguments);

            for (int i = 0; i < phi.NumArguments; i++)
            {
                var source = phi.Sources[i];
                var value = phi.GetValue<Value>(i);

                if (chainInfo.InnerBlocks.Contains(source))
                {
                    // All inner sources have the same value (verified in FindPhisToRemap)
                    innerValue ??= value;
                }
                else
                {
                    // Keep external sources as-is
                    externalSources.Add((source, value));
                }
            }

            // If we have inner sources, we need to create a new phi or replace it
            if (innerValue is not null)
            {
                var exitTransform = transform.GetBasicBlockTransform(phi.BasicBlock);

                // Create new phi with consolidated sources:
                // - One source from the entry block (for all inner blocks)
                // - Keep all external sources
                var phiBuilder = exitTransform.CreatePhi(phi.Location, phi.Type);

                // Add the consolidated inner source
                var newEntryBlock = transform.GetBasicBlockTransform(chainInfo.EntryBlock).BasicBlock;
                phiBuilder.AddArgument(
                    newEntryBlock,
                    transform.Rewrite(innerValue).AsNotNull());

                // Add all external sources
                foreach (var (source, value) in externalSources)
                {
                    var newSourceBlock = transform.GetBasicBlockTransform(source).BasicBlock;
                    phiBuilder.AddArgument(
                        newSourceBlock,
                        transform.Rewrite(value).AsNotNull());
                }

                // Seal and replace the old phi
                var newPhi = phiBuilder.Seal();
                transform.Replace(phi, newPhi);
            }
        }
    }

    /// <summary>
    /// Recursively builds a compound boolean condition from an if-chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? BuildCompoundCondition(
        BasicBlockTransform blockTransform,
        BasicBlock entryBlock,
        ConditionalView conditionalView,
        HashSet<BasicBlock> innerBlocks,
        BasicBlock trueExit,
        BasicBlock falseExit)
    {
        // Build condition recursively following the if-chain structure
        // The old implementation built: (a & b & c) | (d & e & f)
        return BuildConditionRecursive(
            blockTransform,
            conditionalView.TrueTarget,
            conditionalView.FalseTarget,
            blockTransform.Rewrite(entryBlock.TerminationCondition!),
            null,
            innerBlocks,
            trueExit,
            falseExit,
            out _);
    }

    /// <summary>
    /// Recursively builds conditions for the if-chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value? BuildConditionRecursive(
        BasicBlockTransform blockTransform,
        BasicBlock trueTarget,
        BasicBlock falseTarget,
        Value? currentCondition,
        Value? accumulatedExitCondition,
        HashSet<BasicBlock> innerBlocks,
        BasicBlock trueExit,
        BasicBlock falseExit,
        out Value? exitCondition)
    {
        exitCondition = accumulatedExitCondition;

        // Check if we've reached an exit block
        if (trueTarget == trueExit || trueTarget == falseExit)
        {
            // Update exit condition if we reached the true exit
            if (trueTarget == trueExit && currentCondition is not null)
            {
                exitCondition = accumulatedExitCondition is null
                    ? currentCondition
                    : blockTransform.CreateArithmetic(
                        currentCondition.Location,
                        accumulatedExitCondition,
                        currentCondition,
                        BinaryArithmeticKind.Or);
            }
            return exitCondition;
        }

        // Continue traversing if this is an inner block
        if (innerBlocks.Contains(trueTarget) &&
            trueTarget.TryGetConditionalView(out var trueConditionalView))
        {
            var trueCondition = blockTransform.Rewrite(trueConditionalView.Condition);
            var mergedTrue = currentCondition is not null && trueCondition is not null
                ? blockTransform.CreateArithmetic(
                    currentCondition.Location,
                    currentCondition,
                    trueCondition,
                    BinaryArithmeticKind.And)
                : trueCondition;

            BuildConditionRecursive(
                blockTransform,
                trueConditionalView.TrueTarget,
                trueConditionalView.FalseTarget,
                mergedTrue,
                exitCondition,
                innerBlocks,
                trueExit,
                falseExit,
                out exitCondition);
        }

        // Handle false target if needed
        if (falseTarget != falseExit &&
            innerBlocks.Contains(falseTarget) &&
            falseTarget.TryGetConditionalView(out var falseConditionalView))
        {
            var falseCondition = blockTransform.Rewrite(falseConditionalView.Condition);
            var notCondition = currentCondition is not null
                ? blockTransform.CreateArithmetic(
                    currentCondition.Location,
                    currentCondition,
                    UnaryArithmeticKind.Not)
                : null;

            var mergedFalse = notCondition is not null && falseCondition is not null
                ? blockTransform.CreateArithmetic(
                    notCondition.Location,
                    notCondition,
                    falseCondition,
                    BinaryArithmeticKind.And)
                : falseCondition;

            BuildConditionRecursive(
                blockTransform,
                falseConditionalView.TrueTarget,
                falseConditionalView.FalseTarget,
                mergedFalse,
                exitCondition,
                innerBlocks,
                trueExit,
                falseExit,
                out exitCondition);
        }

        return exitCondition;
    }
}
