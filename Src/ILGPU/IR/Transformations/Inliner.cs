// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Inliner.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents an abstract inlining configuration.
    /// </summary>
    public interface IInliningConfiguration
    {
        /// <summary>
        /// Returns true if the given callee function can be inlined.
        /// </summary>
        /// <param name="caller">The caller.</param>
        /// <param name="methodCall">The actual function call.</param>
        /// <param name="callee">The callee function.</param>
        /// <returns>True, if the given calle function can be inlined.</returns>
        bool CanInline(
            Landscape.Entry caller,
            MethodCall methodCall,
            Scope callee);
    }

    /// <summary>
    /// Contains generic inliner helpers and default configurations.
    /// </summary>
    public static class Inliner
    {
        #region Nested Types

        /// <summary>
        /// Represents an inling configuration to inline all functions
        /// (except those marked with NoInlining).
        /// </summary>
        public readonly struct AggressiveInliningConfiguration : IInliningConfiguration
        {
            /// <summary cref="IInliningConfiguration.CanInline(Landscape.Entry, MethodCall, Scope)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanInline(
                Landscape.Entry caller,
                MethodCall methodCall,
                Scope callee)
            {
                // Try to find an aggressive inlining attribute
                if (callee.Method.HasFlags(MethodFlags.NoInlining))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Represents an inling configuration to inline functions marked
        /// with "aggressive inlining" only.
        /// </summary>
        public readonly struct ConservativeInliningConfiguration : IInliningConfiguration
        {
            /// <summary cref="IInliningConfiguration.CanInline(Landscape.Entry, MethodCall, Scope)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanInline(
                Landscape.Entry caller,
                MethodCall methodCall,
                Scope callee) =>
                callee.Method.HasFlags(MethodFlags.AggressiveInlining);
        }

        /// <summary>
        /// Represents a default (but slightly aggressive) inlining configuration.
        /// </summary>
        public readonly struct DefaultInliningConfiguration : IInliningConfiguration
        {
            private const int MaxNumBlocks = 16;

            /// <summary cref="IInliningConfiguration.CanInline(Landscape.Entry, MethodCall, Scope)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanInline(
                Landscape.Entry caller,
                MethodCall methodCall,
                Scope callee)
            {
                var calleeMethod = callee.Method;

                // Try to find an aggressive inlining attribute
                if (calleeMethod.HasFlags(MethodFlags.NoInlining))
                    return false;
                if (calleeMethod.HasFlags(MethodFlags.AggressiveInlining))
                    return true;

                return callee.Count < MaxNumBlocks;
            }
        }

        /// <summary>
        /// Represents a no inlining configuration.
        /// </summary>
        public readonly struct NoInliningConfiguration : IInliningConfiguration
        {
            /// <summary cref="IInliningConfiguration.CanInline(Landscape.Entry, MethodCall, Scope)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanInline(
                Landscape.Entry caller,
                MethodCall methodCall,
                Scope callee) => false;
        }

        #endregion

        #region Static

        /// <summary>
        /// Represents an inling configuration to inline all functions
        /// (except those marked with NoInlining).
        /// </summary>
        public static readonly AggressiveInliningConfiguration AggressiveInlining = default;

        /// <summary>
        /// Represents an inling configuration to inline functions marked
        /// with "aggressive inlining" only.
        /// </summary>
        public static readonly ConservativeInliningConfiguration ConservativeInlining = default;

        /// <summary>
        /// Represents a default (but slightly aggressive) inlining configuration.
        /// </summary>
        public static readonly DefaultInliningConfiguration Default = default;

        /// <summary>
        /// Represents a no inlining configuration.
        /// </summary>
        public static readonly NoInliningConfiguration NoInlining = default;

        /// <summary>
        /// Adds a new inliner pass to the given builder.
        /// </summary>
        /// <typeparam name="TInliningConfiguration">The configuration type.</typeparam>
        /// <param name="builder">The current transformer builder.</param>
        /// <param name="inliningConfiguration">The inlining configuration.</param>
        public static void AddInliner<TInliningConfiguration>(
            this Transformer.Builder builder,
            TInliningConfiguration inliningConfiguration)
            where TInliningConfiguration : IInliningConfiguration
        {
            builder.Add(new Inliner<TInliningConfiguration>(
                inliningConfiguration));
        }

        /// <summary>
        /// Adds a new inliner pass to the given builder.
        /// </summary>
        /// <param name="builder">The current transformer builder.</param>
        /// <param name="flags">The current context flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddInliner(this Transformer.Builder builder, ContextFlags flags)
        {
            if (flags.HasFlags(ContextFlags.AggressiveInlining))
                builder.AddInliner(AggressiveInlining);
            else if (flags.HasFlags(ContextFlags.ConservativeInlining))
                builder.AddInliner(ConservativeInlining);
            else if (!flags.HasFlags(ContextFlags.NoInlining))
                builder.AddInliner(Default);
        }

        #endregion
    }

    /// <summary>
    /// Represents a function inliner.
    /// </summary>
    /// <typeparam name="TConfiguration">The configuration type.</typeparam>
    public sealed class Inliner<TConfiguration> : OrderedTransformation
        where TConfiguration : IInliningConfiguration
    {
        private readonly TConfiguration configuration;

        /// <summary>
        /// Constructs a new specializer.
        /// </summary>
        public Inliner(in TConfiguration inliningConfiguration)
        {
            configuration = inliningConfiguration;
        }

        /// <summary cref="OrderedTransformation.PerformTransformation{TScopeProvider}(Method.Builder, Landscape, Landscape{object}.Entry, TScopeProvider)"/>
        protected override bool PerformTransformation<TScopeProvider>(
            Method.Builder builder,
            Landscape landscape,
            Landscape.Entry current,
            TScopeProvider scopeProvider)
        {
            if (!current.HasReferences)
                return false;

            var processed = new HashSet<BasicBlock>();
            var toProcess = new Stack<BasicBlock>();

            bool result = false;
            var currentBlock = current.Scope.EntryBlock;

            while (true)
            {
                if (processed.Add(currentBlock))
                {
                    if (result = InlineCalls(
                        builder,
                        current,
                        scopeProvider,
                        ref currentBlock))
                    {
                        result = true;
                        continue;
                    }

                    var successors = currentBlock.Successors;
                    if (successors.Length > 0)
                    {
                        currentBlock = successors[0];
                        for (int i = 1, e = successors.Length; i < e; ++i)
                            toProcess.Push(successors[1]);
                        continue;
                    }
                }

                if (toProcess.Count < 1)
                    break;
                currentBlock = toProcess.Pop();
            }

            return result;
        }

        /// <summary>
        /// Tries to inline method calls.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="caller">The parent caller entry.</param>
        /// <param name="scopeProvider">The scope provider.</param>
        /// <param name="currentBlock">The current block (may be modified).</param>
        /// <returns>True, in case of an inlined call.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InlineCalls<TScopeProvider>(
            Method.Builder builder,
            Landscape.Entry caller,
            TScopeProvider scopeProvider,
            ref BasicBlock currentBlock)
            where TScopeProvider : IScopeProvider
        {
            foreach (var valueEntry in currentBlock)
            {
                if (!(valueEntry.Value is MethodCall call))
                    continue;

                var targetScope = scopeProvider[call.Target];
                if (configuration.CanInline(caller, call, targetScope))
                {
                    var blockBuilder = builder[currentBlock];
                    var tempBlock = blockBuilder.SpecializeCall(call, targetScope);

                    // We can continue our search in the temp block
                    currentBlock = tempBlock.BasicBlock;
                    return true;
                }
            }

            return false;
        }
    }
}
