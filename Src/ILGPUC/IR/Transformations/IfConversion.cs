// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PostDominators = ILGPUC.IR.Analyses.Dominators<
    ILGPUC.IR.MethodValues.Backwards>;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Converts nested if/switch branches into value conditionals (predicated execution).
/// </summary>
/// <remarks>
/// This transformation identifies simple if/else branches where both paths have no side
/// effects or collectives and are small enough, then converts them to predicated
/// instructions using conditional values (select/predicate operations). This enables
/// better instruction level parallelism and reduces branch mis-prediction costs.
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
        var candidates = new List<ConversionInfo>();
        foreach (var block in method.Blocks)
        {
            var analyzed = TryAnalyzeBranch(block, postDominators);
            if (analyzed.HasValue)
                candidates.Add(analyzed.Value);
        }

        // Drop the branches whose phis cannot become predicates until the
        // remaining ones agree on the phis that become predicates
        var convertedPhis = new Dictionary<PhiValue, ConversionInfo>();
        int numCandidates;
        do
        {
            numCandidates = candidates.Count;
            convertedPhis.Clear();
            foreach (var info in candidates)
            {
                foreach (var phi in info.PhisToConvert)
                    convertedPhis[phi] = info;
            }
            candidates.RemoveAll(info => !IsPredicable(info, convertedPhis));
        }
        while (candidates.Count != numCandidates);

        var result = method.CreateMap<BasicBlock, ConversionInfo>();
        foreach (var info in candidates)
            result.Add(info.BranchBlock, info);
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

            // Reject blocks with side effects or collectives
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
    /// Checks if a block has side effects or collectives (warp and group
    /// operations), whose results depend on the threads executing them: only the
    /// threads taking the branch may execute them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSideEffects(BasicBlock block)
    {
        foreach (var value in block.Values)
        {
            if (value is MemoryValue or MethodCall or ThreadValue)
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
        // Note: block.Values includes phi values, regular values, AND the
        // termination value; PhiValues is the collection restricted to phis.
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
    /// Returns true if the phis of the given branch can become predicates, which
    /// evaluate their arguments unconditionally, while the regions of the branch
    /// become dead. Every value the arguments reach must be a value defined
    /// outside the regions, a phi of a nested branch that becomes a predicate as
    /// well, or a pure value without an integer div/rem that can fault
    /// (speculating a division above its guard faults on the CPU backend — a
    /// miscompile, never a budget question). Pure values reachable from the
    /// branch condition itself were already unconditional and are exempt.
    /// Iterative (deep pure DAGs must not overflow the thread stack).
    /// </summary>
    /// <remarks>
    /// The predicate of a nested branch evaluates its condition and its
    /// arguments, which this branch guarded, so they are checked like the
    /// arguments of this branch.
    /// </remarks>
    /// <param name="info">The branch to check.</param>
    /// <param name="convertedPhis">The phis of all branches to convert.</param>
    private static bool IsPredicable(
        in ConversionInfo info,
        Dictionary<PhiValue, ConversionInfo> convertedPhis)
    {
        if (info.PhisToConvert.Count == 0)
            return true;

        var alreadyUnconditional = new HashSet<Value>();
        CollectPureValues(info.BranchBlock.TerminationCondition, alreadyUnconditional);

        var visited = new HashSet<Value>();
        var pending = new Stack<Value>();
        foreach (var phi in info.PhisToConvert)
        {
            foreach (var argument in phi.Arguments)
                pending.Push(argument);
        }

        while (pending.Count > 0)
        {
            var value = pending.Pop();
            if (!visited.Add(value))
                continue;

            switch (value)
            {
                case PureValue pureValue:
                    if (alreadyUnconditional.Contains(pureValue))
                        break;
                    if (pureValue is BinaryArithmeticValue { CanFault: true })
                        return false;
                    foreach (var operand in pureValue.Values)
                        pending.Push(operand);
                    break;
                case BasicBlockValue blockValue when
                    info.TrueRegion.Contains(blockValue.BasicBlock) ||
                    info.FalseRegion.Contains(blockValue.BasicBlock):
                    // A value of the regions survives only as the predicate of
                    // a nested branch
                    if (blockValue is not PhiValue phi ||
                        !convertedPhis.TryGetValue(phi, out var nested))
                    {
                        return false;
                    }
                    foreach (var argument in phi.Arguments)
                        pending.Push(argument);
                    if (nested.BranchBlock.TerminationCondition is { } condition)
                        pending.Push(condition);
                    break;
            }
        }
        return true;
    }

    /// <summary>
    /// Collects all pure values reachable from the given root into the set.
    /// Iterative (deep pure DAGs must not overflow the thread stack).
    /// </summary>
    private static void CollectPureValues(Value? root, HashSet<Value> target)
    {
        var pending = new Stack<Value>();
        if (root is not null)
            pending.Push(root);
        while (pending.Count > 0)
        {
            if (pending.Pop() is not PureValue pureValue || !target.Add(pureValue))
                continue;
            foreach (var operand in pureValue.Values)
                pending.Push(operand);
        }
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
        // This pass registers no OnMap converters, so base.OnTransform's converter
        // walk has nothing to do — phi values must be converted explicitly here.
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
