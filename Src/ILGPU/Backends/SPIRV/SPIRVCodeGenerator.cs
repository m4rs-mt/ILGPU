using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using System.Collections.Generic;
using System.Linq;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// A SPIR-V code generator.
    /// </summary>
    public abstract partial class SPIRVCodeGenerator : IBackendCodeGenerator<ISPIRVBuilder>
    {
        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly struct GeneratorArgs
        {

            // You may be wondering why the id allocator is included as a generator
            // argument instead of SPIRVCodeGenerator inheriting from it.

            // Well, the type generator needs access to the allocator which would mean
            // that the type generator would have to be constructed in the code generator.
            // That's bad because the backend needs access to the type generator too.
            // The idea is to move type generator and allocator to generator args so they
            // can be used here and in the backend.
            internal GeneratorArgs(
                SPIRVBackend backend,
                EntryPoint entryPoint,
                ConcurrentIdProvider provider,
                SPIRVIdAllocator allocator,
                ISPIRVBuilder builder,
                SPIRVTypeGenerator generator,
                in AllocaKindInformation sharedAllocations,
                in AllocaKindInformation dynamicSharedAllocations)
            {
                Backend = backend;
                EntryPoint = entryPoint;
                Builder = builder;
                IdProvider = provider;
                IdAllocator = allocator;
                GeneralTypeGenerator = generator;
                SharedAllocations = sharedAllocations;
                DynamicSharedAllocations = dynamicSharedAllocations;
            }

            /// <summary>
            /// Returns the underlying backend.
            /// </summary>
            public SPIRVBackend Backend { get; }

            /// <summary>
            /// Returns the current entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns the id provider to use
            /// </summary>
            public ConcurrentIdProvider IdProvider { get; }

            /// <summary>
            /// Returns the id allocator to use
            /// </summary>
            public SPIRVIdAllocator IdAllocator { get; }

            /// <summary>
            /// Returns the type generator to use
            /// </summary>
            public SPIRVTypeGenerator GeneralTypeGenerator { get; }

            /// <summary>
            /// Returns the type of SPIRV builder this generator will use.
            /// </summary>
            public ISPIRVBuilder Builder { get; }

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

        /// <summary>
        /// Constructs a new code generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="method">The method to generate.</param>
        /// <param name="allocas">The allocas to generate.</param>
        internal SPIRVCodeGenerator(in GeneratorArgs args, Method method, Allocas allocas)
        {
            Builder = args.Builder;
            IdProvider = args.IdProvider;
            IdAllocator = args.IdAllocator;
            GeneralTypeGenerator = args.GeneralTypeGenerator;
            Method = method;
            Allocas = allocas;
        }

        private Dictionary<BasicBlock, uint> _labels = new Dictionary<BasicBlock, uint>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated SPIR-V Builder.
        /// </summary>
        public ISPIRVBuilder Builder { get; }

        /// <summary>
        /// Returns the id provider to use
        /// </summary>
        public ConcurrentIdProvider IdProvider { get; }

        /// <summary>
        /// Returns the associated id allocator
        /// </summary>
        public SPIRVIdAllocator IdAllocator { get; }

        /// <summary>
        /// Returns the associated type generator.
        /// </summary>
        public SPIRVTypeGenerator GeneralTypeGenerator { get; }

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
        public abstract void GenerateHeader(ISPIRVBuilder builder);

        /// <summary>
        /// Generates SPIR-V code.
        /// </summary>
        public abstract void GenerateCode();

        /// <summary>
        /// Generates code for generic blocks.
        /// </summary>
        /// <remarks>
        /// This is for use by classes like the <see cref="SPIRVFunctionGenerator"/>.
        /// </remarks>
        protected void GenerateGeneralCode()
        {
            var blocksWithIds = Method.Blocks.Select(b => (b, IdProvider.Next()));

            // Generate code
            foreach (var (block, id) in blocksWithIds)
            {
                _labels.Add(block, id);
                Builder.GenerateOpLabel(id);

                foreach (var value in block)
                {
                    this.GenerateCodeFor(value);
                }

                // Build terminator
                this.GenerateCodeFor(block.Terminator);
            }
        }

        /// <summary>
        /// Generates SPIR-V constant declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateConstants(ISPIRVBuilder builder)
        {
            // No constants to emit
        }

        /// <summary cref="IBackendCodeGenerator{TKernelBuilder}.Merge(TKernelBuilder)"/>
        public void Merge(ISPIRVBuilder builder) =>
            builder.Merge(Builder);

        #endregion
    }
}
