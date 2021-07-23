using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Linq;

namespace ILGPU.Backends.SPIRV
{
    public class SPIRVFunctionGenerator : SPIRVCodeGenerator
    {
        #region Instance

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

        private uint _methodId;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void GenerateHeader(ISPIRVBuilder builder)
        {
            uint methodId = IdProvider.Next();
            uint returnTypeId = GeneralTypeGenerator[Method.ReturnType];

            var parameters = Method.Parameters
                .Select(x => GeneralTypeGenerator[x.ParameterType])
                .ToArray();

            builder.GenerateOpTypeFunction(methodId, returnTypeId, parameters);
            _methodId = methodId;
        }

        /// <inheritdoc />
        public override void GenerateCode()
        {
            GenerateFunctionStart();
            GenerateGeneralCode();
            GenerateFunctionEnd();
        }

        private void GenerateFunctionStart()
        {
            var method = IdProvider.Next();
            var returnType = GeneralTypeGenerator[Method.ReturnType];

            var control = Method.HasFlags(MethodFlags.Inline)
                ? FunctionControl.Inline
                : FunctionControl.None;

            Builder.GenerateOpFunction(method, returnType, control, _methodId);
        }

        private void GenerateFunctionEnd() => Builder.GenerateOpFunctionEnd();

        #endregion
    }
}
