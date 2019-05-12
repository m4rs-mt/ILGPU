// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(ReturnTerminator)"/>
        public void Visit(ReturnTerminator returnTerminator)
        {
            if (!returnTerminator.IsVoidReturn)
            {
                var resultRegister = Load(returnTerminator.ReturnValue);
                EmitStoreParam(returnParamName, resultRegister);
            }
            Command(Instructions.ReturnOperation);
        }

        /// <summary cref="IValueVisitor.Visit(UnconditionalBranch)"/>
        public void Visit(UnconditionalBranch branch)
        {
            using (var command = BeginCommand(Instructions.BranchOperation))
            {
                var targetLabel = blockLookup[branch.Target];
                command.AppendLabel(targetLabel);
            }
        }

        /// <summary cref="IValueVisitor.Visit(ConditionalBranch)"/>
        public void Visit(ConditionalBranch branch)
        {
            var condition = LoadPrimitive(branch.Condition);
            using (var command = BeginCommand(
                Instructions.BranchOperation,
                new PredicateConfiguration(condition, true)))
            {
                var trueLabel = blockLookup[branch.TrueTarget];
                command.AppendLabel(trueLabel);
            }

            // Jump to false target in the else case
            using (var command = BeginCommand(Instructions.BranchOperation))
            {
                var targetLabel = blockLookup[branch.FalseTarget];
                command.AppendLabel(targetLabel);
            }
        }

        /// <summary cref="IValueVisitor.Visit(SwitchBranch)"/>
        public void Visit(SwitchBranch branch)
        {
            var idx = LoadPrimitive(branch.Condition);
            using (var lowerBoundsScope = new PredicateScope(this))
            {
                // Emit less than
                var lessThanCommand = Instructions.GetCompareOperation(
                    CompareKind.LessThan,
                    ArithmeticBasicValueType.Int32);
                using (var command = BeginCommand(
                    lessThanCommand))
                {
                    command.AppendArgument(lowerBoundsScope.PredicateRegister);
                    command.AppendArgument(idx);
                    command.AppendConstant(0);
                }

                using (var upperBoundsScope = new PredicateScope(this))
                {
                    using (var command = BeginCommand(
                        Instructions.BranchIndexRangeComparison))
                    {
                        command.AppendArgument(upperBoundsScope.PredicateRegister);
                        command.AppendArgument(idx);
                        command.AppendConstant(branch.NumCasesWithoutDefault);
                        command.AppendArgument(lowerBoundsScope.PredicateRegister);
                    }
                    using (var command = BeginCommand(
                        Instructions.BranchOperation,
                        new PredicateConfiguration(upperBoundsScope.PredicateRegister, true)))
                    {
                        var defaultTarget = blockLookup[branch.DefaultBlock];
                        command.AppendLabel(defaultTarget);
                    }
                }
            }

            var targetLabel = DeclareLabel();
            MarkLabel(targetLabel);
            Builder.Append('\t');
            Builder.Append(Instructions.BranchTargetsDeclaration);
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
                Instructions.BranchIndexOperation))
            {
                command.AppendArgument(idx);
                command.AppendLabel(targetLabel);
            }
        }
    }
}
