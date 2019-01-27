// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Inliner.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a function inliner.
    /// </summary>
    public sealed class Inliner : OrderedTransformation
    {
        #region Constants

        /// <summary>
        /// The maximum number of IL instructions to inline.
        /// </summary>
        private const int MaxNumILInstructionsToInline = 32;

        #endregion

        #region Static

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetupInliningAttributes(
            IRContext context,
            Method method,
            DisassembledMethod disassembledMethod)
        {
            // Check whether we are allowed to inline this method
            if (context.HasFlags(ContextFlags.NoInlining))
                return;

            if (method.HasSource)
            {
                var source = method.Source;
                if ((source.MethodImplementationFlags & MethodImplAttributes.NoInlining)
                    == MethodImplAttributes.NoInlining)
                    return;

                if ((source.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining)
                    == MethodImplAttributes.AggressiveInlining ||
                    source.Module.Name == Context.AssemblyModuleName)
                    method.AddFlags(MethodFlags.Inline);
            }

            // Evaluate a simple inlining heuristic
            if (context.HasFlags(ContextFlags.AggressiveInlining) ||
                disassembledMethod.Instructions.Length <= MaxNumILInstructionsToInline)
            {
                method.AddFlags(MethodFlags.Inline);
            }
        }

        #endregion

        /// <summary>
        /// Constructs a new inliner that inlines all methods marked with
        /// <see cref="MethodFlags.Inline"/> flags.
        /// </summary>
        public Inliner() { }

        /// <summary cref="OrderedTransformation.PerformTransformation{TScopeProvider}(Method.Builder, Landscape, Landscape{object}.Entry, TScopeProvider)"/>
        protected override bool PerformTransformation<TScopeProvider>(
            Method.Builder builder,
            Landscape landscape,
            Landscape.Entry current,
            TScopeProvider scopeProvider)
        {
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
        private static bool InlineCalls<TScopeProvider>(
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

                if (call.Target.HasFlags(MethodFlags.Inline))
                {
                    var targetScope = scopeProvider[call.Target];
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
