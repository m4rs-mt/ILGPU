using ILGPU.IR.Values;
using System;

namespace ILGPU.Backends.SPIRV
{
    public partial class SPIRVCodeGenerator
    {
        public void GenerateCode(ReturnTerminator returnTerminator) =>
            throw new NotImplementedException();

        public void GenerateCode(UnconditionalBranch branch) =>
            throw new NotImplementedException();

        public void GenerateCode(IfBranch branch) => throw new NotImplementedException();

        public void GenerateCode(SwitchBranch branch) =>
            throw new NotImplementedException();
    }
}
