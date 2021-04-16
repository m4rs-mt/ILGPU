using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.OpenCL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Collections.Generic;
using System.Text;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// A SPIR-V code generator.
    /// </summary>
    public abstract partial class SPIRVCodeGenerator :
        SPIRVIdAllocator,
        IBackendCodeGenerator<SPIRVBuilder>
    {
        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly struct GeneratorArgs
        {
            internal GeneratorArgs(
                SPIRVBackend backend,
                EntryPoint entryPoint,
                SPRIVTypeGenerator generator,
                in AllocaKindInformation sharedAllocations,
                in AllocaKindInformation dynamicSharedAllocations)
            {
                Backend = backend;
                EntryPoint = entryPoint;
                TypeGenerator = generator;
                SharedAllocations = sharedAllocations;
                DynamicSharedAllocations = dynamicSharedAllocations;
            }

            /// <summary>
            /// Returns the underlying backend.
            /// </summary>
            public SPIRVBackend Backend { get; }

            /// <summary>
            /// Returns the type generator
            /// </summary>
            public SPRIVTypeGenerator TypeGenerator { get; }

            /// <summary>
            /// Returns the current entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns all shared allocations.
            /// </summary>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns all dynamic shared allocations.
            /// </summary>
            public AllocaKindInformation DynamicSharedAllocations { get; }
        }

        #endregion

        #region Instance

        private readonly Dictionary<BasicBlock, uint> blockLookup =
            new Dictionary<BasicBlock, uint>();

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="method">The method to generate.</param>
        /// <param name="allocas">The allocas to generate.</param>
        internal SPIRVCodeGenerator(in GeneratorArgs args, Method method, Allocas allocas)
            : base(args.Backend)
        {
            Builder = new SPIRVBuilder();
            Method = method;
            Allocas = allocas;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated SPIR-V Builder
        /// </summary>
        public SPIRVBuilder Builder { get; }

        /// <summary>
        /// Returns the associated method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns all local allocas.
        /// </summary>
        public Allocas Allocas { get; }

        #endregion

        #region IBackendCodeGenerator

        /// <summary>
        /// Generates a function declaration in SPIR-V code.
        /// </summary>
        public abstract void GenerateHeader(SPIRVBuilder builder);

        /// <summary>
        /// Generates SPIR-V code.
        /// </summary>
        public abstract void GenerateCode();

        /// <summary>
        /// Generates SPIR-V constant declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateConstants(SPIRVBuilder builder)
        {
            // No constants to emit
        }

        /// <summary cref="IBackendCodeGenerator{TKernelBuilder}.Merge(TKernelBuilder)"/>
        public void Merge(SPIRVBuilder builder) =>
            builder.Instructions.AddRange(Builder.Instructions);

        #endregion
    }
}
