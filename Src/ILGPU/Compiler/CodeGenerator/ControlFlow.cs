// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: ControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a return instruction.
        /// </summary>
        private void MakeReturn()
        {
            if (Method.IsVoid)
                BuildRetVoid(Builder);
            else
            {
                var returnValue = CurrentBlock.Pop(Method.ReturnType);
                BuildRet(Builder, returnValue.LLVMValue);
            }
        }

        /// <summary>
        /// Realizes an uncoditional branch instruction.
        /// </summary>
        /// <param name="targets">The jump targets.</param>
        private void MakeBranch(ILInstructionBranchTargets targets)
        {
            var block = bbMapping[targets.UnconditionalBranchTarget.Value];
            BuildBr(Builder, block.LLVMBlock);
        }

        /// <summary>
        /// Realizes a conditional branch instruction.
        /// </summary>
        /// <param name="targets">The jump targets.</param>
        /// <param name="compareType">The comparison type of the condition.</param>
        /// <param name="unsigned">True, .</param>
        private void MakeBranch(ILInstructionBranchTargets targets, CompareType compareType, bool unsigned)
        {
            var condition = CreateCompare(compareType, unsigned);
            var ifBlock = bbMapping[targets.ConditionalBranchIfTarget.Value];
            var elseBlock = bbMapping[targets.ConditionalBranchElseTarget.Value];
            BuildCondBr(
                Builder, 
                condition.LLVMValue,
                ifBlock.LLVMBlock,
                elseBlock.LLVMBlock);
        }

        /// <summary>
        /// Realizes a switch instruction.
        /// </summary>
        /// <param name="targets">The jump targets.</param>
        private unsafe void MakeSwitch(ILInstructionBranchTargets targets)
        {
            var switchValue = CurrentBlock.PopInt();
            var defaultBlock = bbMapping[targets.SwitchDefaultTarget.Value];
            var @switch = BuildSwitch(
                Builder, 
                switchValue.LLVMValue,
                defaultBlock.LLVMBlock,
                targets.Count - 1);
            for (int i = 1, e = targets.Count; i < e; ++i)
            {
                var block = bbMapping[targets[i]];
                AddCase(
                    @switch,
                    ConstInt(LLVMContext.Int32Type, i - 1, true), block.LLVMBlock);
            }
        }
    }
}
