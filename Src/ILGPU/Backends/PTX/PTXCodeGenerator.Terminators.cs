// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        /// <summary cref="IBackendCodeGenerator.GenerateCode(ReturnTerminator)"/>
        public void GenerateCode(ReturnTerminator returnTerminator)
        {
            if (!returnTerminator.IsVoidReturn)
            {
                var resultRegister = Load(returnTerminator.ReturnValue);
                EmitStoreParam(ReturnParamName, resultRegister);
            }

            Command(
                Uniforms.IsUniform(returnTerminator)
                    ? PTXInstructions.UniformReturnOperation
                    : PTXInstructions.ReturnOperation);
        }

        private bool NeedSeparatePhiBindings(
            BasicBlock basicBlock,
            BasicBlock target,
            [NotNullWhen(true)]
            out PhiBindings.PhiBindingCollection bindings)
        {
            if (!phiBindings.TryGetBindings(target, out bindings))
                return false;

            // Check whether there are bindings pointing to different blocks
            foreach (var (phiValue, _) in bindings)
            {
                if (phiValue.BasicBlock != target)
                    return true;
            }

            // We were not able to find misleading data
            return false;
        }

        /// <summary>
        /// Generates phi bindings for jumping to a specific target block.
        /// </summary>
        /// <param name="current">The current block.</param>
        private void GeneratePhiBindings(BasicBlock current)
        {
            if (!phiBindings.TryGetBindings(current, out var bindings))
                return;
            BindPhis(bindings, target: null);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(UnconditionalBranch)"/>
        public void GenerateCode(UnconditionalBranch branch)
        {
            // Bind phis
            GeneratePhiBindings(branch.BasicBlock);

            if (Schedule.IsImplicitSuccessor(branch.BasicBlock, branch.Target))
                return;

            // Determine the branch operation to be used
            var branchOperation = Uniforms.IsUniform(branch)
                ? PTXInstructions.UniformBranchOperation
                : PTXInstructions.BranchOperation;

            using var command = BeginCommand(branchOperation);
            var targetLabel = blockLookup[branch.Target];
            command.AppendLabel(targetLabel);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IfBranch)"/>
        public void GenerateCode(IfBranch branch)
        {
            var primitiveCondition = LoadPrimitive(branch.Condition);
            var condition = EnsureHardwareRegister(primitiveCondition);

            // Use the actual branch targets from the schedule
            var (trueTarget, falseTarget) = branch.NotInvertedBranchTargets;

            // Determine the branch operation to be used
            var branchOperation = Uniforms.IsUniform(branch)
                ? PTXInstructions.UniformBranchOperation
                : PTXInstructions.BranchOperation;

            // Gather phi bindings and test both, true and false targets
            if (phiBindings.TryGetBindings(branch.BasicBlock, out var bindings) &&
                (bindings.NeedSeparateBindingsFor(trueTarget) ||
                bindings.NeedSeparateBindingsFor(falseTarget)))
            {
                // We need to emit different bindings in each branch
                if (branch.IsInverted)
                    Utilities.Swap(ref trueTarget, ref falseTarget);

                // Declare a temporary jump target to skip true branches
                var tempLabel = DeclareLabel();
                using (var command = BeginCommand(
                           branchOperation,
                           new PredicateConfiguration(condition, isTrue: false)))
                {
                    command.AppendLabel(tempLabel);
                }

                // Bind all true phis
                BindPhis(bindings, trueTarget);

                // Jump to true target in the current case
                using (var command = BeginCommand(branchOperation))
                {
                    var targetLabel = blockLookup[trueTarget];
                    command.AppendLabel(targetLabel);
                }

                // Mark the false case label and bind all values
                MarkLabel(tempLabel);
                BindPhis(bindings, falseTarget);

                if (!Schedule.IsImplicitSuccessor(branch.BasicBlock, falseTarget))
                {
                    // Jump to false target in the else case
                    using var command = BeginCommand(branchOperation);
                    var targetLabel = blockLookup[falseTarget];
                    command.AppendLabel(targetLabel);
                }

                // Skip further bindings an branches
                return;
            }

            // Generate phi bindings for all blocks
            BindPhis(bindings, target: null);

            // The current schedule has inverted all if conditions with implicit branch
            // targets to simplify the work of the PTX assembler
            if (Schedule.IsImplicitSuccessor(branch.BasicBlock, trueTarget))
            {
                // Jump to false target in the else case
                using var command = BeginCommand(
                    branchOperation,
                    new PredicateConfiguration(
                        condition,
                        isTrue: branch.IsInverted));
                var targetLabel = blockLookup[falseTarget];
                command.AppendLabel(targetLabel);
            }
            else
            {
                if (branch.IsInverted)
                    Utilities.Swap(ref trueTarget, ref falseTarget);
                using (var command = BeginCommand(
                    branchOperation,
                    new PredicateConfiguration(condition, isTrue: true)))
                {
                    var targetLabel = blockLookup[trueTarget];
                    command.AppendLabel(targetLabel);
                }

                // Jump to false target in the else case
                using (var command = BeginCommand(PTXInstructions.UniformBranchOperation))
                {
                    var targetLabel = blockLookup[falseTarget];
                    command.AppendLabel(targetLabel);
                }
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SwitchBranch)"/>
        public void GenerateCode(SwitchBranch branch)
        {
            bool isUniform = Uniforms.IsUniform(branch);
            var idx = LoadPrimitive(branch.Condition);
            using (var lowerBoundsScope = new PredicateScope(this))
            {
                // Emit less than
                var lessThanCommand = PTXInstructions.GetCompareOperation(
                    CompareKind.LessThan,
                    CompareFlags.None,
                    ArithmeticBasicValueType.Int32);
                using (var command = BeginCommand(
                    lessThanCommand))
                {
                    command.AppendArgument(lowerBoundsScope.PredicateRegister);
                    command.AppendArgument(idx);
                    command.AppendConstant(0);
                }

                using var upperBoundsScope = new PredicateScope(this);
                using (var command = BeginCommand(
                    PTXInstructions.BranchIndexRangeComparison))
                {
                    command.AppendArgument(upperBoundsScope.PredicateRegister);
                    command.AppendArgument(idx);
                    command.AppendConstant(branch.NumCasesWithoutDefault);
                    command.AppendArgument(lowerBoundsScope.PredicateRegister);
                }
                using (var command = BeginCommand(
                    isUniform
                        ? PTXInstructions.UniformBranchOperation
                        : PTXInstructions.BranchOperation,
                    new PredicateConfiguration(
                        upperBoundsScope.PredicateRegister,
                        true)))
                {
                    var defaultTarget = blockLookup[branch.DefaultBlock];
                    command.AppendLabel(defaultTarget);
                }
            }

            var targetLabel = DeclareLabel();
            MarkLabel(targetLabel);
            Builder.Append('\t');
            Builder.Append(PTXInstructions.BranchTargetsDeclaration);
            Builder.Append(' ');
            for (int i = 0, e = branch.NumCasesWithoutDefault; i < e; ++i)
            {
                var caseTarget = branch.GetCaseTarget(i);
                var caseLabel = blockLookup[caseTarget];
                Builder.Append(caseLabel);
                if (i + 1 < e)
                    Builder.Append(", ");
            }
            Builder.AppendLine(";");

            // Generate all phi bindings for all cases
            GeneratePhiBindings(branch.BasicBlock);

            using (var command = BeginCommand(
                isUniform
                    ? PTXInstructions.UniformBranchIndexOperation
                    : PTXInstructions.BranchIndexOperation))
            {
                command.AppendArgument(idx);
                command.AppendLabel(targetLabel);
            }
        }
    }
}
