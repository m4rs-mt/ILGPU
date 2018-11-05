// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File IntrinsicSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// The basic configuration interface for all intrinsic specializers.
    /// </summary>
    public interface IIntrinsicSpecializerConfiguration : IFunctionImportSpecializer
    {
        /// <summary>
        /// Returns true if assertions are enabled.
        /// </summary>
        bool EnableAssertions { get; }

        /// <summary>
        /// Returns the current warp size.
        /// </summary>
        int WarpSize { get; }

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

        /// <summary>
        /// Callback that is invoked when a new intrinsic is imported.
        /// </summary>
        /// <param name="topLevelFunction">The new top level function.</param>
        void OnImportIntrinsic(TopLevelFunction topLevelFunction);
    }

    /// <summary>
    /// Represents an intrinsic implementation specializer.
    /// </summary>
    public sealed class IntrinsicSpecializer<TConfiguration> : UnorderedTransformation
        where TConfiguration : IIntrinsicSpecializerConfiguration
    {
        #region Nested Types

        /// <summary>
        /// Represents a specialization context.
        /// </summary>
        private readonly struct SpecializationContext : IImplFunctionSpecializationContext
        {
            /// <summary>
            /// Constructs a new specialization context.
            /// </summary>
            /// <param name="dominators">The current dominators.</param>
            /// <param name="placement">The current placement information.</param>
            public SpecializationContext(
                Dominators dominators,
                Placement placement)
            {
                Debug.Assert(dominators != null, "Invalid dominators");
                Debug.Assert(placement != null, "Invalid placement");

                Dominators = dominators;
                Placement = placement;
            }

            /// <summary cref="IImplFunctionSpecializationContext.Dominators"/>
            public Dominators Dominators { get; }

            /// <summary cref="IImplFunctionSpecializationContext.Placement"/>
            public Placement Placement { get; }
        }

        #endregion

        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.All;

        private TConfiguration configuration;

        /// <summary>
        /// Constructs a new intrinsic specializer.
        /// </summary>
        public IntrinsicSpecializer(in TConfiguration specializerConfiguration)
            : base(TransformationFlags.SpecializeIntrinsics, FollowUpFlags)
        {
            configuration = specializerConfiguration;
        }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);

            // Mark all functions as dirty in order to rebuild nested functions
            // with specialized intrinsics
            topLevelFunction.AddTransformationFlags(
                TopLevelFunctionTransformationFlags.Dirty);

            var dependencies = ImportDependencies(builder, scope, out bool applied);
            if (dependencies.Length < 1)
                return applied;

            // Refresh scope
            scope = Scope.Create(builder, topLevelFunction);
            var cfg = CFG.Create(scope);
            var dominators = Dominators.Create(cfg);
            var placement = Placement.CreateCSEPlacement(dominators);

            var context = new SpecializationContext(dominators, placement);
            foreach (var (node, implementationFunction) in dependencies)
            {
                builder.SpecializeNodeWithImplFunction(
                    node,
                    implementationFunction,
                    context);
            }

            return true;
        }

        /// <summary>
        /// Analyzes the given scope while importing the required dependencies.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="applied">True, if the transformation transformed something.</param>
        /// <returns>The imported dependency functions.</returns>
        private (Value, TopLevelFunction)[] ImportDependencies(
            IRBuilder builder,
            Scope scope,
            out bool applied)
        {
            var intrinsicFunctions = new List<(Value, TopLevelFunction)>(scope.Count >> 2);
            var implementationResolver = configuration.ImplementationResolver;
            Debug.Assert(implementationResolver != null, "Invalid implementation resolver");
            applied = false;

            // Analyze intrinsic nodes
            foreach (var node in scope)
            {
                switch (node)
                {
                    case DebugTrace debugTrace:
                        // Ignore trace events in kernels for now
                        MemoryRef.Unlink(debugTrace);
                        applied = true;
                        break;
                    case DebugAssertFailed debugAssert:
                        // Check whether assertions are enabled
                        if (configuration.EnableAssertions &&
                            implementationResolver.TryGetDebugImplementation(
                                out TopLevelFunction debugFunction))
                        {
                            intrinsicFunctions.Add((debugAssert, debugFunction));
                        }
                        else
                        {
                            MemoryRef.Unlink(debugAssert);
                            applied = true;
                        }
                        break;
                    case WarpSizeValue warpSizeValue:
                        {
                            var nativeWarpSize = configuration.WarpSize;
                            Debug.Assert(nativeWarpSize > 0, "Invalid native warp size");
                            var primitiveSize = builder.CreatePrimitiveValue(nativeWarpSize);
                            warpSizeValue.Replace(primitiveSize);
                            applied = true;
                        }
                        break;
                    case SizeOfValue sizeOfValue:
                        if (configuration.TryGetSizeOf(sizeOfValue.TargetType, out int size))
                        {
                            var primitiveSize = builder.CreatePrimitiveValue(size);
                            node.Replace(primitiveSize);
                            applied = true;
                        }
                        break;
                    case UnaryArithmeticValue unary:
                        if (implementationResolver.TryGetMathImplementation(
                            unary.Kind,
                            unary.ArithmeticBasicValueType,
                            out TopLevelFunction unaryMethod))
                        {
                            intrinsicFunctions.Add((unary, unaryMethod));
                        }
                        break;
                    case BinaryArithmeticValue binary:
                        if (implementationResolver.TryGetMathImplementation(
                            binary.Kind,
                            binary.ArithmeticBasicValueType,
                            out TopLevelFunction binaryMethod))
                        {
                            intrinsicFunctions.Add((binary, binaryMethod));
                        }
                        break;
                }
            }

            // Import dependencies
            var result = new (Value, TopLevelFunction)[intrinsicFunctions.Count];
            var importedFunctions = new Dictionary<TopLevelFunction, TopLevelFunction>();
            var intrinsicContext = implementationResolver.IntrinsicContext;
            int offset = 0;
            foreach (var (node, intrinsic) in intrinsicFunctions)
            {
                if (!importedFunctions.TryGetValue(intrinsic, out TopLevelFunction imported))
                {
                    if (!builder.Context.TryGetFunction(
                        intrinsic.Handle,
                        out TopLevelFunction resolved))
                    {
                        imported = builder.Import(
                            intrinsicContext,
                            intrinsic,
                            configuration);
                        configuration.OnImportIntrinsic(imported);
                    }

                    importedFunctions.Add(intrinsic, resolved);
                }
                result[offset++] = (node, imported);
            }

            applied |= result.Length > 0;
            return result;
        }
    }
}
