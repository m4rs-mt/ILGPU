// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a return instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private void MakeReturn(Block block, IRBuilder builder)
        {
            var returnType = Builder.Method.ReturnType;

            if (returnType.IsVoidType)
                builder.CreateReturn();
            else
                builder.CreateReturn(block.Pop(returnType, ConvertFlags.None));
        }

        /// <summary>
        /// Realizes an uncoditional branch instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private static void MakeBranch(Block block, IRBuilder builder)
        {
            var targets = block.GetBuilderTerminator(1);

            builder.CreateBranch(targets[0]);
        }

        /// <summary>
        /// Realizes a conditional branch instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="compareKind">The comparison type of the condition.</param>
        /// <param name="instructionFlags">The instruction flags.</param>
        private static void MakeBranch(
            Block block,
            IRBuilder builder,
            CompareKind compareKind,
            ILInstructionFlags instructionFlags)
        {
            var targets = block.GetBuilderTerminator(2);

            var condition = CreateCompare(block, builder, compareKind, instructionFlags);
            builder.CreateIfBranch(condition, targets[0], targets[1]);
        }

        /// <summary>
        /// Make an intrinsic branch.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="kind">The current compare kind.</param>
        private static void MakeIntrinsicBranch(
            Block block,
            IRBuilder builder,
            CompareKind kind)
        {
            var targets = block.GetBuilderTerminator(2);

            var comparisonValue = block.PopCompareValue(ConvertFlags.None);
            var rightValue = builder.CreatePrimitiveValue(comparisonValue.BasicValueType, 0);

            var condition = CreateCompare(builder, comparisonValue, rightValue, kind, CompareFlags.None);
            builder.CreateIfBranch(condition, targets[0], targets[1]);
        }

        /// <summary>
        /// Make a true branch.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private static void MakeBranchTrue(Block block, IRBuilder builder) =>
            MakeIntrinsicBranch(block, builder, CompareKind.NotEqual);

        /// <summary>
        /// Make a false branch.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        private static void MakeBranchFalse(Block block, IRBuilder builder) =>
            MakeIntrinsicBranch(block, builder, CompareKind.Equal);

        /// <summary>
        /// Realizes a switch instruction.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="branchTargets">All switch branch targets.</param>
        private static void MakeSwitch(
            Block block,
            IRBuilder builder,
            ILInstructionBranchTargets branchTargets)
        {
            var targets = block.GetBuilderTerminator(branchTargets.Count);

            var switchValue = block.PopInt(ConvertFlags.TargetUnsigned);

            builder.CreateSwitchBranch(switchValue, targets);
        }
    }
}
