// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;

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
            Command(PTXInstructions.ReturnOperation);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(UnconditionalBranch)"/>
        public void GenerateCode(UnconditionalBranch branch)
        {
            using var command = BeginCommand(PTXInstructions.BranchOperation);
            var targetLabel = blockLookup[branch.Target];
            command.AppendLabel(targetLabel);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IfBranch)"/>
        public void GenerateCode(IfBranch branch)
        {
            var primitiveCondition = LoadPrimitive(branch.Condition);
            var condition = EnsureHardwareRegister(primitiveCondition);
            using (var command = BeginCommand(
                PTXInstructions.BranchOperation,
                new PredicateConfiguration(condition, true)))
            {
                var trueLabel = blockLookup[branch.TrueTarget];
                command.AppendLabel(trueLabel);
            }

            // Jump to false target in the else case
            using (var command = BeginCommand(PTXInstructions.BranchOperation))
            {
                var targetLabel = blockLookup[branch.FalseTarget];
                command.AppendLabel(targetLabel);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SwitchBranch)"/>
        public void GenerateCode(SwitchBranch branch)
        {
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
                    PTXInstructions.BranchOperation,
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

            using (var command = BeginCommand(
                PTXInstructions.BranchIndexOperation))
            {
                command.AppendArgument(idx);
                command.AppendLabel(targetLabel);
            }
        }
    }
}
