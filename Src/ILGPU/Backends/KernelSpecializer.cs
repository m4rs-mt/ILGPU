// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;

namespace ILGPU.Backends
{
    /// <summary>
    /// The basic configuration interface for all intrsic specializers.
    /// </summary>
    public interface IKernelSpecializerConfiguration
        : ISizeOfABI
        , IFunctionImportSpecializer
    {
        /// <summary>
        /// Returns true if assertions are enabled.
        /// </summary>
        bool EnableAssertions { get; }

        /// <summary>
        /// Returns the warp size of the target accelerator.
        /// </summary>
        int WarpSize { get; }

        /// <summary>
        /// Tries to specialize a kernel parameter.
        /// </summary>
        /// <param name="builder">The IR builder.</param>
        /// <param name="functionBuilder">The function builder.</param>
        /// <param name="parameter">The parameter to specialize.</param>
        /// <returns>Null, iff the node could not be specialized. The node otherwise.</returns>
        Value SpecializeKernelParameter(
            IRBuilder builder,
            FunctionBuilder functionBuilder,
            Parameter parameter);

        /// <summary>
        /// Tries to resolve the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="size">The native size in bytes.</param>
        /// <returns>True, if the size could be resolved.</returns>
        bool TryGetSizeOf(TypeNode type, out int size);

        /// <summary>
        /// Returns the associated math implementation resolver.
        /// </summary>
        IntrinsicImplementationResolver ImplementationResolver { get; }
    }

    /// <summary>
    /// Specializes kernels.
    /// </summary>
    /// <typeparam name="TConfiguration">The configuration type.</typeparam>
    public sealed class KernelSpecializer<TConfiguration>
        where TConfiguration : IKernelSpecializerConfiguration
    {
        #region Nested Types

        /// <summary>
        /// Represents a single context that is used during specialization.
        /// </summary>
        private readonly struct SpecializationContext
        {
            /// <summary>
            /// Constructs a new specialization context.
            /// </summary>
            /// <param name="builder">The current IR builder.</param>
            /// <param name="dominators">The dominators.</param>
            /// <param name="placement">The placement.</param>
            public SpecializationContext(
                IRBuilder builder,
                Dominators dominators,
                Placement placement)
            {
                Builder = builder;
                Dominators = dominators;
                Placement = placement;
            }

            #region Properties

            /// <summary>
            /// Returns the current builder.
            /// </summary>
            public IRBuilder Builder { get; }

            /// <summary>
            /// Returns the dominators.
            /// </summary>
            public Dominators Dominators { get; }

            /// <summary>
            /// Returns the current placement.
            /// </summary>
            public Placement Placement { get; }

            #endregion
        }

        /// <summary>
        /// Intrinsic configuration that is used during the actual specialization step.
        /// </summary>
        private sealed class ImportIntrinsics : IIntrinsicSpecializerConfiguration
        {
            private readonly TConfiguration configuration;

            public ImportIntrinsics(in TConfiguration currentConfiguration)
            {
                configuration = currentConfiguration;
            }

            /// <summary>
            /// Returns the number of imported intrinsics.
            /// </summary>
            public int IntrinsicCount { get; private set; }

            /// <summary>
            /// Resets the number of intrinsics.
            /// </summary>
            public void Reset() => IntrinsicCount = 0;

            /// <summary cref="IIntrinsicSpecializerConfiguration.EnableAssertions"/>
            public bool EnableAssertions => configuration.EnableAssertions;

            /// <summary cref="IIntrinsicSpecializerConfiguration.WarpSize"/>
            public int WarpSize => configuration.WarpSize;

            /// <summary cref="IIntrinsicSpecializerConfiguration.ImplementationResolver"/>
            public IntrinsicImplementationResolver ImplementationResolver => configuration.ImplementationResolver;

            /// <summary cref="IIntrinsicSpecializerConfiguration.TryGetSizeOf(TypeNode, out int)"/>
            public bool TryGetSizeOf(TypeNode type, out int size) =>
                configuration.TryGetSizeOf(type, out size);

            /// <summary cref="IIntrinsicSpecializerConfiguration.OnImportIntrinsic(TopLevelFunction)"/>
            void IIntrinsicSpecializerConfiguration.OnImportIntrinsic(TopLevelFunction topLevelFunction) =>
                ++IntrinsicCount;

            public void Map(
                IRContext sourceContext,
                TopLevelFunction sourceFunction,
                IRBuilder builder,
                IRRebuilder rebuilder) =>
                configuration.Map(sourceContext, sourceFunction, builder, rebuilder);
        }

        #endregion

        #region Instance

        private TConfiguration configuration;
        private readonly ImportIntrinsics importIntrinsics;
        private readonly Transformer transformation;

        /// <summary>
        /// Constructs a new kernel specializer.
        /// </summary>
        /// <param name="specializerConfiguration">The specializer configuration.</param>
        public KernelSpecializer(
            in TConfiguration specializerConfiguration)
        {
            configuration = specializerConfiguration;

            importIntrinsics = new ImportIntrinsics(specializerConfiguration);
            transformation = Transformer.Create(
                new TransformerConfiguration(TopLevelFunctionTransformationFlags.None, false),
                new Transformer.TransformSpecification(
                    new IntrinsicSpecializer<ImportIntrinsics>(importIntrinsics), 1));
       }

        #endregion

        /// <summary>
        /// Returns the used import specification.
        /// </summary>
        public ContextImportSpecification ImportSpecification =>
            new ContextImportSpecification();

        /// <summary>
        /// Prepares the given kernel function in the scope
        /// of the target context.
        /// </summary>
        /// <param name="kernelFunction">The kernel function.</param>
        /// <param name="targetContext">The target context.</param>
        public void PrepareKernel(
            ref TopLevelFunction kernelFunction,
            IRContext targetContext)
        {
            importIntrinsics.Reset();
            transformation.Transform(targetContext, 1);
            targetContext.RefreshFunction(ref kernelFunction);

            if (importIntrinsics.IntrinsicCount > 0)
            {
                var reachableFunctions = ImmutableArray.Create(kernelFunction);
                targetContext.UnloadUnreachableMethods(reachableFunctions);
            }

            // Perform specialization transformation
            using (var builder = targetContext.CreateBuilder(IRBuilderFlags.None))
            {
                var newTargetFunction = builder.CreateFunction(kernelFunction.Declaration);

                kernelFunction.MemoryParam.Replace(newTargetFunction.MemoryParam);
                kernelFunction.ReturnParam.Replace(newTargetFunction.ReturnParam);

                foreach (var param in kernelFunction.Parameters)
                {
                    var newNode = configuration.SpecializeKernelParameter(
                        builder,
                        newTargetFunction,
                        param);
                    if (newNode == null)
                        newNode = newTargetFunction.AddParameter(param.Type, param.Name);
                    param.Replace(newNode);
                }

                // Wire body
                newTargetFunction.Seal(kernelFunction.Target);
            }

            // Cleanup IR
            targetContext.GC();
        }
    }
}
