using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System;
using System.Text;

namespace ILGPU.Backends.SPIRV
{
    public class SPIRVBackend : CodeGeneratorBackend<
        SPIRVIntrinsic.Handler,
        SPIRVCodeGenerator.GeneratorArgs,
        SPIRVCodeGenerator,
        ISPIRVBuilder>
    {

        #region Nested Types

        /// <summary>
        /// The type of ISPIRVBuilder to use
        /// </summary>
        public enum SPIRVBuilderType
        {
            Binary,
            StringRepresentation
        }

        #endregion

        #region Instance

        private readonly SPIRVBuilderType _builderType;

        public SPIRVBackend(
            Context context,
            CapabilityContext capabilities,
            BackendType backendType,
            BackendFlags backendFlags,
            ArgumentMapper argumentMapper,
            SPIRVBuilderType builderType) :
            base(context,
                capabilities,
                backendType,
                backendFlags,
                argumentMapper)
        {
            _builderType = builderType;
        }

        #endregion

        protected override ISPIRVBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out SPIRVCodeGenerator.GeneratorArgs data)
        {
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            ISPIRVBuilder builder = _builderType switch
            {
                SPIRVBuilderType.Binary => new BinarySPIRVBuilder(),
                SPIRVBuilderType.StringRepresentation => new StringSPIRVBuilder(),
                _ => new BinarySPIRVBuilder()
            };

            const uint magicNumber = 0x07230203;
            const uint version = 0x00010200;
            const uint ILGPUGeneratorMagicNumber = 0x10101010;
            const uint schema = 0;

            //TODO: Calculate bound
            builder.AddMetadata(
                magicNumber,
                version,
                ILGPUGeneratorMagicNumber,
                0,
                schema
            );

            data = new SPIRVCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                new SPRIVTypeGenerator(),
                builder,
                backendContext.SharedAllocations,
                backendContext.DynamicSharedAllocations);
            return builder;
        }

        protected override SPIRVCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            SPIRVCodeGenerator.GeneratorArgs data) =>
            throw new NotImplementedException();

        protected override SPIRVCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            SPIRVCodeGenerator.GeneratorArgs data) => throw new NotImplementedException();

        protected override CompiledKernel CreateKernel(EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
            ISPIRVBuilder builder, SPIRVCodeGenerator.GeneratorArgs data) =>
            throw new NotImplementedException();
    }
}
