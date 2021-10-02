// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IBackendCodeGenerator.GenerateCode(ReturnTerminator)"/>
        public void GenerateCode(ReturnTerminator returnTerminator)
        {
            using var statement = BeginStatement(CLInstructions.ReturnStatement);
            if (!returnTerminator.IsVoidReturn)
            {
                var resultRegister = Load(returnTerminator.ReturnValue);
                statement.AppendArgument(resultRegister);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(UnconditionalBranch)"/>
        public void GenerateCode(UnconditionalBranch branch) =>
            GotoStatement(branch.Target);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IfBranch)"/>
        public void GenerateCode(IfBranch branch)
        {
            // TODO: refactor if-block generation into a separate emitter
            // See also EmitImplicitKernelIndex

            var condition = Load(branch.Condition);
            if (condition is ConstantVariable constantVariable)
            {
                if (constantVariable.Value.RawValue != 0)
                    GotoStatement(branch.TrueTarget);
                else
                    GotoStatement(branch.FalseTarget);
            }
            else
            {
                AppendIndent();
                Builder.Append("if (");
                Builder.Append(condition.ToString());
                Builder.AppendLine(")");
                PushIndent();
                GotoStatement(branch.TrueTarget);
                PopIndent();
                GotoStatement(branch.FalseTarget);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SwitchBranch)"/>
        public void GenerateCode(SwitchBranch branch)
        {
            var condition = Load(branch.Condition);
            var indentStr = new string('\t', Indent);

            using var statement = BeginStatement($"switch ({condition}) {{\n");
            for (int i = 0, e = branch.NumCasesWithoutDefault; i < e; ++i)
            {
                statement.AppendOperation("{0}case {1}:\n{0}\t{2} {3};\n",
                    indentStr,
                    i,
                    CLInstructions.GotoStatement,
                    branch.GetCaseTarget(i));
            }
            statement.AppendOperation("{0}default:\n{0}\t{1} {2};\n{0}}}",
                indentStr,
                CLInstructions.GotoStatement,
                branch.Targets[0]);
        }
    }
}
