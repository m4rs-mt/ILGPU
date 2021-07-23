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
                var variable = IdAllocator.Load(returnTerminator.ReturnValue);
                Builder.GenerateOpReturnValue(variable);
            }
        }

        public void GenerateCode(UnconditionalBranch branch)
        {
            var target = _labels[branch.Target];
            Builder.GenerateOpBranch(target);
        }

        public void GenerateCode(IfBranch branch)
        {
            var condition = IdAllocator.Load(branch.Condition);
            var trueTarget = _labels[branch.TrueTarget];
            var falseTarget = _labels[branch.FalseTarget];

            Builder.GenerateOpBranchConditional(
                condition,
                trueTarget,
                falseTarget);
        }

        public void GenerateCode(SwitchBranch branch)
        {
            var selector = IdAllocator.Load(branch.Condition);
            var defaultBranch = _labels[branch.DefaultBlock];
            var switchTargets = new List<PairLiteralIntegerIdRef>();
            for (int i = 0; i < branch.NumCasesWithoutDefault; i++)
            {
                var target = branch.GetCaseTarget(i);
                var targetVar = _labels[target];
                switchTargets.Add(new PairLiteralIntegerIdRef
                {
                    base0 = (uint) i, // Case Number
                    base1 =  targetVar // Case Target
                });
            }
            Builder.GenerateOpSwitch(
                selector,
                defaultBranch,
                switchTargets.ToArray());
        }
    }
}
