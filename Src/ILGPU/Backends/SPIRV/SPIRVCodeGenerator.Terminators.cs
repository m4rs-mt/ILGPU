using ILGPU.IR.Values;
using System.Collections.Generic;

namespace ILGPU.Backends.SPIRV
{
    public partial class SPIRVCodeGenerator
    {
        public void GenerateCode(ReturnTerminator returnTerminator)
        {
            if (returnTerminator.IsVoidReturn)
            {
                Builder.GenerateOpReturn();
            }
            else
            {
                var variable = Load(returnTerminator.ReturnValue);
                Builder.GenerateOpReturnValue(variable);
            }
        }

        public void GenerateCode(UnconditionalBranch branch)
        {
            var target = Load(branch.Target);
            Builder.GenerateOpBranch(target);
        }

        public void GenerateCode(IfBranch branch)
        {
            var condition = Load(branch.Condition);
            var trueTarget = Load(branch.TrueTarget);
            var falseTarget = Load(branch.FalseTarget);

            Builder.GenerateOpBranchConditional(
                condition,
                trueTarget,
                falseTarget);
        }

        public void GenerateCode(SwitchBranch branch)
        {
            var selector = Load(branch.Condition);
            var defaultBranch = Load(branch.DefaultBlock);
            var switchTargets = new List<PairLiteralIntegerIdRef>();
            for (int i = 0; i < branch.NumCasesWithoutDefault; i++)
            {
                var target = branch.GetCaseTarget(i);
                var targetVar = Load(target);
                switchTargets.Add(new PairLiteralIntegerIdRef
                {
                    base0 = (uint) i,
                    base1 =  targetVar
                });
            }
            Builder.GenerateOpSwitch(
                selector,
                defaultBranch,
                switchTargets.ToArray());
        }
    }
}
