// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Inliner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
            // Check whether we can inline this method
            if (!method.HasImplementation)
                return;

            if (method.HasSource)
            {
                var source = method.Source;
                if ((source.MethodImplementationFlags &
                    MethodImplAttributes.NoInlining) ==
                    MethodImplAttributes.NoInlining)
                {
                    return;
                }

                if ((source.MethodImplementationFlags &
                    MethodImplAttributes.AggressiveInlining) ==
                    MethodImplAttributes.AggressiveInlining ||
                    source.Module.Name == Context.FullAssemblyModuleName)
                {
                    method.AddFlags(MethodFlags.Inline);
                }
            }

            // Evaluate a simple inlining heuristic
            if (context.Properties.InliningMode != InliningMode.Conservative ||
                disassembledMethod.Instructions.Length <= MaxNumILInstructionsToInline)
            {
                method.AddFlags(MethodFlags.Inline);
            }
        }

        /// <summary>
        /// Tries to inline method calls.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="currentBlock">The current block (may be modified).</param>
        /// <returns>True, in case of an inlined call.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InlineCalls(
            Method.Builder builder,
            ref BasicBlock currentBlock)
        {
            foreach (var valueEntry in currentBlock)
            {
                if (!(valueEntry.Value is MethodCall call))
                    continue;

                if (call.Target.HasFlags(MethodFlags.Inline))
                {
                    var blockBuilder = builder[currentBlock];
                    var tempBlock = blockBuilder.SpecializeCall(call);

                    // We can continue our search in the temp block
                    currentBlock = tempBlock.BasicBlock;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new inliner that inlines all methods marked with
        /// <see cref="MethodFlags.Inline"/> flags.
        /// </summary>
        public Inliner() { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the inlining transformation.
        /// </summary>
        protected override bool PerformTransformation(
            IRContext context,
            Method.Builder builder,
            Landscape landscape,
            Landscape.Entry current)
        {
            var processed = builder.SourceBlocks.CreateSet();
            var toProcess = new Stack<BasicBlock>();

            bool result = false;
            var currentBlock = builder.EntryBlock;

            while (true)
            {
                if (processed.Add(currentBlock))
                {
                    if (result = InlineCalls(builder, ref currentBlock))
                    {
                        result = true;
                        continue;
                    }

                    var successors = currentBlock.CurrentSuccessors;
                    if (successors.Length > 0)
                    {
                        currentBlock = successors[0];
                        for (int i = 1, e = successors.Length; i < e; ++i)
                            toProcess.Push(successors[i]);
                        continue;
                    }
                }

                if (toProcess.Count < 1)
                    break;
                currentBlock = toProcess.Pop();
            }

            return result;
        }

        #endregion

    }
}
