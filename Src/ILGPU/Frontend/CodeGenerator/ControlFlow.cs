// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a return instruction.
        /// </summary>
        private void MakeReturn()
        {
            var returnType = MethodBuilder.Method.ReturnType;

            if (returnType.IsVoidType)
            {
                Builder.CreateReturn(Location);
            }
            else
            {
                Builder.CreateReturn(
                    Location,
                    Block.Pop(returnType, ConvertFlags.None));
            }
        }

        /// <summary>
        /// Realizes an unconditional branch instruction.
        /// </summary>
        private void MakeBranch()
        {
            var targets = Block.GetBuilderTerminator(1);
            Builder.CreateBranch(Location, targets[0]);
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
            var targets = Block.GetBuilderTerminator(2);

            var condition = CreateCompare(compareKind, instructionFlags);
            Builder.CreateIfBranch(
                Location,
                condition,
                targets[0],
                targets[1]);
        }

        /// <summary>
        /// Make an intrinsic branch.
        /// </summary>
        /// <param name="kind">The current compare kind.</param>
        private void MakeIntrinsicBranch(CompareKind kind)
        {
            var targets = Block.GetBuilderTerminator(2);

            var comparisonValue = Block.PopCompareValue(Location, ConvertFlags.None);
            var rightValue = Builder.CreatePrimitiveValue(
                Location,
                comparisonValue.BasicValueType,
                0);

            var condition = CreateCompare(
                comparisonValue,
                rightValue,
                kind,
                CompareFlags.None);
            Builder.CreateIfBranch(
                Location,
                condition,
                targets[0],
                targets[1]);
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
            var targets = Block.GetBuilderTerminator(branchTargets.Count);

            var switchValue = Block.PopInt(Location, ConvertFlags.TargetUnsigned);
            var targetList = targets.ToInlineList();
            Builder.CreateSwitchBranch(
                Location,
                switchValue,
                ref targetList);
        }
    }
}
