// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: IntrinsicSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Flags for the <see cref="IntrinsicSpecializer{TDelegate}"/> transformation.
    /// </summary>
    [Flags]
    public enum IntrinsicSpecializerFlags : int
    {
        /// <summary>
        /// Default lowering flags.
        /// </summary>
        None,

        /// <summary>
        /// Enables assertions.
        /// </summary>
        EnableAssertions = 1 << 0,
    }

    /// <summary>
    /// Represents an intrinsic implementation specializer.
    /// </summary>
    /// <remarks>
    /// Note that this class does not perform recursive specialization operations.
    /// </remarks>
    /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
    public sealed class IntrinsicSpecializer<TDelegate> : UnorderedTransformation<CachedScopeProvider>
        where TDelegate : Delegate
    {
        #region Utility Methods

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

        #endregion

        private readonly IntrinsicImplementationProvider<TDelegate> provider;

        /// <summary>
        /// Constructs a new intrinsic specializer.
        /// </summary>
        public IntrinsicSpecializer(
            IntrinsicSpecializerFlags flags,
            IntrinsicImplementationProvider<TDelegate> implementationProvider)
        {
            Flags = flags;
            provider = implementationProvider;
        }

        /// <summary>
        /// Returns the current flags.
        /// </summary>
        public IntrinsicSpecializerFlags Flags { get; }

        /// <summary>
        /// Returns true if assertions should be enabled.
        /// </summary>
        public bool EnableAssertions =>
            (Flags & IntrinsicSpecializerFlags.EnableAssertions) !=
            IntrinsicSpecializerFlags.None;

        /// <summary cref="UnorderedTransformation{TIntermediate}.CreateIntermediate"/>
        protected override CachedScopeProvider CreateIntermediate() => new CachedScopeProvider();

        /// <summary cref="UnorderedTransformation{TIntermediate}.FinishProcessing(TIntermediate)"/>
        protected override void FinishProcessing(CachedScopeProvider intermediate) { }

        /// <summary cref="UnorderedTransformation{TIntermediate}.PerformTransformation(Method.Builder, TIntermediate)"/>
        protected override bool PerformTransformation(
            Method.Builder builder,
            CachedScopeProvider scopeProvider)
        {
            // Check whether we are currently processing an intrinsic method
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
            applied = false;

            // Analyze intrinsic nodes
            foreach (Value value in scope.Values)
            {
                var blockBuilder = builder[value.BasicBlock];

                if (value is DebugOperation debug)
                {
                    if (EnableAssertions &&
                        provider.TryGetImplementation(debug, out var debugImplementation))
                        intrinsicFunctions.Add((debug, debugImplementation));
                    else
                        blockBuilder.Remove(debug);
                    applied = true;
                }
                // Check intrinsic value
                else if (provider.TryGetImplementation(value, out var implementation))
                {
                    intrinsicFunctions.Add((value, implementation));
                    applied = true;
                }
            }

            return intrinsicFunctions;
        }
    }
}
