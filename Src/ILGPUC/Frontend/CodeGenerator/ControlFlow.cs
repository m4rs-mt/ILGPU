// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Realizes a return instruction.
    /// </summary>
    private void MakeReturn()
    {
        var returnType = MethodBuilder.Method.Type;

        if (returnType is VoidType)
            Builder.CreateReturnTermination();
        else
            Builder.CreateReturnTermination(Block.Pop(returnType, ConvertFlags.None));
    }

    /// <summary>
    /// Realizes an unconditional branch instruction.
    /// </summary>
    private void MakeBranch()
    {
        var successors = Block.GetPendingSuccessors(1);
        Builder.CreateUnconditionalTermination(successors[0]);
    }

    /// <summary>
    /// Realizes a conditional branch instruction.
    /// </summary>
    /// <param name="compareKind">The comparison type of the condition.</param>
    /// <param name="instructionFlags">The instruction flags.</param>
    private void MakeBranch(
        CompareKind compareKind,
        ILInstructionFlags instructionFlags)
    {
        var successors = Block.GetPendingSuccessors(2);

        var condition = CreateCompare(compareKind, instructionFlags);
        Builder.CreateConditionalTermination(
            condition,
            successors[0],
            successors[1]);
    }

    /// <summary>
    /// Make an intrinsic branch.
    /// </summary>
    /// <param name="kind">The current compare kind.</param>
    private void MakeIntrinsicBranch(CompareKind kind)
    {
        var comparisonValue = Block.PopCompareValue(Location, ConvertFlags.None);

        var successors = Block.GetPendingSuccessors(2);

        // Delegate caching pattern: when the comparison value is a NullValue
        // (from compiler-generated closure class field loads), the comparison
        // `null != null` is always false. Fold directly to an unconditional
        // branch and clean up the dead target's predecessor edges.
        if (comparisonValue is NullValue)
        {
            // brtrue (NotEqual): null != 0 -> false -> take false branch
            // brfalse (Equal): null == 0 -> true -> take true branch
            bool isTrue = kind != CompareKind.NotEqual;
            var liveTarget = isTrue ? successors[0] : successors[1];
            var deadTarget = isTrue ? successors[1] : successors[0];

            Builder.CreateUnconditionalTermination(liveTarget);

            // The CFGBuilder's SetupPredecessors wired edges based on the
            // pending termination which included both targets. Now that the
            // branch is folded, remove the dead edge and propagate to any
            // blocks that become unreachable.
            CleanupDeadPredecessorEdge(Block.BasicBlock, deadTarget);
            return;
        }

        // For pointer types, compare against a null of the same pointer type
        // instead of a primitive zero to avoid Int64 → ptr conversion failures.
        var rightValue = comparisonValue.Type is AddressSpaceType
            ? Builder.CreateNull(Location, comparisonValue.Type)
            : Builder.CreatePrimitiveValue(
                Location,
                comparisonValue.BasicValueType,
                0);

        var condition = CreateCompare(
            comparisonValue,
            rightValue,
            kind,
            CompareFlags.None);
        Builder.CreateConditionalTermination(
            condition,
            successors[0],
            successors[1]);
    }

    /// <summary>
    /// Cleans up predecessor edges after a branch is folded to unconditional.
    /// Removes <paramref name="source"/> from <paramref name="deadTarget"/>'s
    /// predecessors, then propagates: if a block becomes unreachable (zero
    /// predecessors and not the method entry), its own successor edges are
    /// also removed.
    /// </summary>
    /// <param name="source">The block whose branch was folded.</param>
    /// <param name="deadTarget">The target that is no longer branched to.</param>
    private static void CleanupDeadPredecessorEdge(
        BasicBlock source,
        BasicBlock deadTarget)
    {
        deadTarget.RemovePredecessor(source);

        // If the dead target still has other predecessors it's reachable
        // through another path — nothing more to do.
        if (deadTarget.Predecessors.Length > 0)
            return;

        // The block is now unreachable. Remove it as a predecessor from
        // all of its successors (propagate transitively).
        foreach (var successor in deadTarget.Successors)
            CleanupDeadPredecessorEdge(deadTarget, successor);
    }

    /// <summary>
    /// Make a true branch.
    /// </summary>
    private void MakeBranchTrue() => MakeIntrinsicBranch(CompareKind.NotEqual);

    /// <summary>
    /// Make a false branch.
    /// </summary>
    private void MakeBranchFalse() => MakeIntrinsicBranch(CompareKind.Equal);

    /// <summary>
    /// Realizes a switch instruction.
    /// </summary>
    /// <param name="branchTargets">All switch branch targets.</param>
    private void MakeSwitch(ILInstructionBranchTargets branchTargets)
    {
        var successors = Block.GetPendingSuccessors(branchTargets.Count);

        var switchValue = Block.PopInt(Location, ConvertFlags.TargetUnsigned);

        // Create switch termination with default (first) and cases (rest)
        var switchBuilder = Builder.CreateSwitchTermination(
            switchValue,
            capacity: successors.Length);
        switchBuilder.AddDefault(successors[0]);
        for (int i = 1; i < successors.Length; i++)
            switchBuilder.AddCase(successors[i]);
        switchBuilder.Seal();
    }
}
