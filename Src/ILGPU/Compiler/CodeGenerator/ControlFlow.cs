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

using ILGPU.Util;
using LLVMSharp;

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
                InstructionBuilder.CreateRetVoid();
            else
            {
                var returnValue = CurrentBlock.Pop(Method.ReturnType);
                InstructionBuilder.CreateRet(returnValue.LLVMValue);
            }
        }

        /// <summary>
        /// Realizes an uncoditional branch instruction.
        /// </summary>
        /// <param name="targets">The jump targets.</param>
        private void MakeBranch(ILInstructionBranchTargets targets)
        {
            var block = bbMapping[targets.UnconditionalBranchTarget.Value];
            InstructionBuilder.CreateBr(block.LLVMBlock);
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
            InstructionBuilder.CreateCondBr(
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
            var @switch = InstructionBuilder.CreateSwitch(
                switchValue.LLVMValue,
                defaultBlock.LLVMBlock,
                (uint)(targets.Count - 1));
            for (int i = 1, e = targets.Count; i < e; ++i)
            {
                var block = bbMapping[targets[i]];
                LLVM.AddCase(
                    @switch,
                    LLVMExtensions.ConstInt(LLVMContext.Int32TypeInContext(), i - 1, true), block.LLVMBlock);
            }
        }
    }
}
