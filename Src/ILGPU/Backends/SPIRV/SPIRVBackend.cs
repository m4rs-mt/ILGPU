using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
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

            var provider = new ConcurrentIdProvider();
            var allocator = new SPIRVIdAllocator(provider);
            var typeGenerator = new SPIRVTypeGenerator(provider);

            data = new SPIRVCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                provider,
                allocator,
                builder,
                typeGenerator,
                backendContext.SharedAllocations,
                backendContext.DynamicSharedAllocations);

            return builder;
        }

        protected override SPIRVCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            SPIRVCodeGenerator.GeneratorArgs data) =>
            new SPIRVFunctionGenerator(data, method, allocas);

        protected override SPIRVCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            SPIRVCodeGenerator.GeneratorArgs data) => throw new NotImplementedException();

        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
            ISPIRVBuilder builder,
            SPIRVCodeGenerator.GeneratorArgs data)
        {
            ISPIRVBuilder typeBuilder = _builderType switch
            {
                SPIRVBuilderType.Binary => new BinarySPIRVBuilder(),
                SPIRVBuilderType.StringRepresentation => new StringSPIRVBuilder(),
                _ => new BinarySPIRVBuilder()
            };

            data.GeneralTypeGenerator.GenerateTypes(typeBuilder);

            // Merge main builder into type builder so the types stay at the start
            typeBuilder.Merge(builder);

            var source = typeBuilder.ToByteArray();

            Debug.Assert(typeBuilder is BinarySPIRVBuilder,
                "You are creating a compiled kernel with string " +
                "source instead of bytecode. Is this a mistake?");

            return new SPIRVCompiledKernel(
                Context,
                entryPoint as SeparateViewEntryPoint,
                kernelInfo,
                source);
        }
    }
}
