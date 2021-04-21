using ILGPU.IR.Values;
using System;

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
                //TODO: Figure out id counting
                var variable = Load(returnTerminator.ReturnValue);
                Builder.GenerateOpReturnValue(0);
            }
        }

        public void GenerateCode(UnconditionalBranch branch)
        {
        }

        public void GenerateCode(IfBranch branch)
        {
            var condition = Load(branch.Condition);
            //Builder.GenerateOpBranchConditional(condition.Id, );
        }

        public void GenerateCode(SwitchBranch branch) =>
            throw new NotImplementedException();
    }
}
