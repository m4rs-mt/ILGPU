// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(ReturnTerminator)"/>
        public void Visit(ReturnTerminator returnTerminator)
        {
            using (var statement = BeginStatement(CLInstructions.ReturnStatement))
            {
                if (!returnTerminator.IsVoidReturn)
                {
                    var resultRegister = Load(returnTerminator.ReturnValue);
                    statement.AppendArgument(resultRegister);
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(UnconditionalBranch)"/>
        public void Visit(UnconditionalBranch branch)
        {
            GotoStatement(branch.Target);
        }

        /// <summary cref="IValueVisitor.Visit(ConditionalBranch)"/>
        public void Visit(ConditionalBranch branch)
        {
            // TODO: refactor if-block generation into a separate emitter
            // See also EmitImplicitKernelIndex

            var condition = Load(branch.Condition);
            AppendIndent();
            Builder.Append("if (");
            Builder.Append(condition.ToString());
            Builder.AppendLine(")");
            PushIndent();
            GotoStatement(branch.TrueTarget);
            PopIndent();
            GotoStatement(branch.FalseTarget);
        }

        /// <summary cref="IValueVisitor.Visit(SwitchBranch)"/>
        public void Visit(SwitchBranch branch)
        {
            var condition = Load(branch.Condition);
            AppendIndent();
            Builder.Append("switch (");
            Builder.Append(condition.ToString());
            Builder.AppendLine(")");

            AppendIndent();
            Builder.AppendLine("{");

            for (int i = 0, e = branch.NumCasesWithoutDefault; i < e; ++i)
            {
                Builder.Append("case ");
                Builder.Append(i.ToString());
                Builder.AppendLine(":");
                PushAndAppendIndent();
                GotoStatement(branch.GetCaseTarget(i));
                PopIndent();
            }

            AppendIndent();
            Builder.AppendLine("default:");
            GotoStatement(branch.Targets[0]);
            Builder.AppendLine("}");
        }
    }
}
