using ILGPU.IR;
using ILGPU.IR.Analyses;

namespace ILGPU.Backends.SPIRV
{
    public class SPIRVFunctionGenerator : SPIRVCodeGenerator
    {
        /// <summary>
        /// Creates a new SPIR-V function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        public SPIRVFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        { }

        public override void GenerateHeader(ISPIRVBuilder builder)
        {

        }

        public override void GenerateCode()
        {

            GenerateGeneralCode();
            Builder.GenerateOpFunctionEnd();
        }

        private void GenerateFunctionHeader()
        {
            var function = Allocate(Method);

            var returnType = Load(Method.ReturnType);
            var control = Method.HasFlags(MethodFlags.Inline)
                ? FunctionControl.Inline
                : FunctionControl.None;
            Builder.GenerateOpFunction(function, returnType, control, );
        }
    }
}
