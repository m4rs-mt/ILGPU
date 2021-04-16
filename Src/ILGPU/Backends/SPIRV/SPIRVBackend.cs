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
        SPIRVBuilder>
    {
        public SPIRVBackend(
            Context context,
            CapabilityContext capabilities,
            BackendType backendType,
            BackendFlags backendFlags,
            ArgumentMapper argumentMapper) :
            base(context,
                capabilities,
                backendType,
                backendFlags,
                argumentMapper)
        {
        }

        protected override SPIRVBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out SPIRVCodeGenerator.GeneratorArgs data)
        {
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            var builder = new SPIRVBuilder();
            const uint MagicNumber = 0x07230203;
            const uint Version = 0x00010200;
            const uint ILGPUGeneratorMagicNumber = 0x10101010;
            const uint Schema = 0;

            builder.Instructions.Add(MagicNumber);
            builder.Instructions.Add(Version);
            builder.Instructions.Add(ILGPUGeneratorMagicNumber);

            //TODO: Calculate bound or insert this after compilation
            builder.Instructions.Add(0);

            builder.Instructions.Add(Schema);

            data = new SPIRVCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                new SPRIVTypeGenerator(),
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
            SPIRVBuilder builder, SPIRVCodeGenerator.GeneratorArgs data) =>
            throw new NotImplementedException();
    }
}
