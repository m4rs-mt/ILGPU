// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File IntrinsicSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// The basic configuration interface for all intrinsic specializers.
    /// </summary>
    public interface IIntrinsicSpecializerConfiguration
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
    }

    /// <summary>
    /// Represents an intrinsic implementation specializer.
    /// </summary>
    public sealed class IntrinsicSpecializer<TConfiguration> : UnorderedTransformation<CachedScopeProvider>
        where TConfiguration : IIntrinsicSpecializerConfiguration
    {
        private TConfiguration configuration;

        /// <summary>
        /// Constructs a new intrinsic specializer.
        /// </summary>
        public IntrinsicSpecializer(in TConfiguration specializerConfiguration)
        {
            configuration = specializerConfiguration;
        }

        /// <summary cref="UnorderedTransformation{TIntermediate}.CreateIntermediate"/>
        protected override CachedScopeProvider CreateIntermediate() => new CachedScopeProvider();

        /// <summary cref="UnorderedTransformation{TIntermediate}.FinishProcessing(TIntermediate)"/>
        protected override void FinishProcessing(CachedScopeProvider intermediate) { }

        /// <summary cref="UnorderedTransformation{TIntermediate}.PerformTransformation(Method.Builder, TIntermediate)"/>
        protected override bool PerformTransformation(Method.Builder builder, CachedScopeProvider scopeProvider)
        {
            var scope = builder.CreateScope();

            var dependencies = FindDependencies(builder, scope, out bool applied);
            if (dependencies.Count < 1)
                return applied;

            // Import all dependencies
            ImportDependencies(builder.Context, dependencies, scopeProvider);

            // Replace every node with a function call to the given implementation function
            foreach (var (node, method) in dependencies)
            {
                var blockBuilder = builder[node.BasicBlock];
                blockBuilder.ReplaceWithCall(node, method);
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
        private List<(Value, Method)> FindDependencies(
            Method.Builder builder,
            Scope scope,
            out bool applied)
        {
            var intrinsicFunctions = new List<(Value, Method)>(scope.Count >> 2);
            var implementationResolver = configuration.ImplementationResolver;
            Debug.Assert(implementationResolver != null, "Invalid implementation resolver");
            applied = false;

            // Analyze intrinsic nodes
            foreach (Value value in scope.Values)
            {
                var blockBuilder = builder[value.BasicBlock];

                switch (value)
                {
                    case DebugTrace debugTrace:
                        // Ignore trace events in kernels for now
                        blockBuilder.Remove(debugTrace);
                        applied = true;
                        break;
                    case DebugAssertFailed debugAssert:
                        // Check whether assertions are enabled
                        if (configuration.EnableAssertions &&
                            implementationResolver.TryGetDebugImplementation(
                                out Method debugFunction))
                        {
                            intrinsicFunctions.Add((debugAssert, debugFunction));
                        }
                        else
                        {
                            blockBuilder.Remove(debugAssert);
                            applied = true;
                        }
                        break;
                    case WarpSizeValue warpSizeValue:
                        {
                            var nativeWarpSize = configuration.WarpSize;
                            Debug.Assert(nativeWarpSize > 0, "Invalid native warp size");
                            var primitiveSize = blockBuilder.CreatePrimitiveValue(nativeWarpSize);
                            warpSizeValue.Replace(primitiveSize);
                            applied = true;
                        }
                        break;
                    case SizeOfValue sizeOfValue:
                        if (configuration.TryGetSizeOf(sizeOfValue.TargetType, out int size))
                        {
                            var primitiveSize = blockBuilder.CreatePrimitiveValue(size);
                            sizeOfValue.Replace(primitiveSize);
                            applied = true;
                        }
                        break;
                    case UnaryArithmeticValue unary:
                        if (implementationResolver.TryGetMathImplementation(
                            unary.Kind,
                            unary.ArithmeticBasicValueType,
                            out Method unaryMethod))
                        {
                            intrinsicFunctions.Add((unary, unaryMethod));
                        }
                        break;
                    case BinaryArithmeticValue binary:
                        if (implementationResolver.TryGetMathImplementation(
                            binary.Kind,
                            binary.ArithmeticBasicValueType,
                            out Method binaryMethod))
                        {
                            intrinsicFunctions.Add((binary, binaryMethod));
                        }
                        break;
                }
            }

            return intrinsicFunctions;
        }

        /// <summary>
        /// Imports all detected dependencies into the current context.
        /// </summary>
        /// <typeparam name="TScopeProvider">The provider to resolve methods to scopes.</typeparam>
        /// <param name="targetContext">The target context.</param>
        /// <param name="dependencies">The dependencies to import.</param>
        /// <param name="scopeProvider">Resolves methods to scopes.</param>
        private static void ImportDependencies<TScopeProvider>(
            IRContext targetContext,
            List<(Value, Method)> dependencies,
            TScopeProvider scopeProvider)
            where TScopeProvider : IScopeProvider
        {
            var importedFunctions = new Dictionary<Method, Method>();
            for (int i = 0, e = dependencies.Count; i < e; ++i)
            {
                var (node, intrinsic) = dependencies[i];
                if (!importedFunctions.TryGetValue(intrinsic, out Method imported))
                {
                    imported = targetContext.Import(intrinsic, scopeProvider);
                    importedFunctions.Add(intrinsic, imported);
                }
                dependencies[i] = (node, imported);
            }
        }
    }
}
