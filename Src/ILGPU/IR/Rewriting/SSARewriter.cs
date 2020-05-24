// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SSARewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Rewriting
{
    /// <summary>
    /// A processor that can be applied to every block.
    /// </summary>
    /// <typeparam name="TVariable">The SSA variable type.</typeparam>
    public readonly struct SSARewriterContext<TVariable> : IRewriterContext
        where TVariable : IEquatable<TVariable>
    {
        #region Instance

        private readonly RewriterContext baseContext;

        internal SSARewriterContext(
            RewriterContext context,
            SSABuilder<TVariable> ssaBuilder)
        {
            baseContext = context;
            SSABuilder = ssaBuilder;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current SSA builder.
        /// </summary>
        public SSABuilder<TVariable> SSABuilder { get; }

        /// <summary>
        /// Returns the associated builder.
        /// </summary>
        public BasicBlock.Builder Builder => baseContext.Builder;

        /// <summary>
        /// Returns the associated block.
        /// </summary>
        public BasicBlock Block => Builder.BasicBlock;

        #endregion

        #region Methods

        /// <summary>
        /// Specializes the build by setting a new block builder.
        /// </summary>
        /// <param name="newBuilder">The new builder to use.</param>
        /// <returns>The specialized rewriter context.</returns>
        public SSARewriterContext<TVariable> SpecializeBuilder(
            BasicBlock.Builder newBuilder) =>
            new SSARewriterContext<TVariable>(
                baseContext.SpecializeBuilder(newBuilder),
                SSABuilder);

        /// <summary>
        /// Returns true if the given value has been converted.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True, if the given value has been converted.</returns>
        public bool IsConverted(Value value) => baseContext.IsConverted(value);

        /// <summary>
        /// Marks the given value as converted.
        /// </summary>
        /// <param name="value">The value to mark.</param>
        /// <returns>True, if the element has been added to the set of value.</returns>
        public bool MarkConverted(Value value) => baseContext.MarkConverted(value);

        /// <summary>
        /// Replaces the given value with the new value.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public TValue Replace<TValue>(Value value, TValue newValue)
            where TValue : Value => baseContext.Replace(value, newValue);

        /// <summary>
        /// Replaces the given value with the new value and removes it from the block.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public TValue ReplaceAndRemove<TValue>(Value value, TValue newValue)
            where TValue : Value => baseContext.ReplaceAndRemove(value, newValue);

        /// <summary>
        /// Removes the given value from the block.
        /// </summary>
        /// <param name="value">The current value.</param>
        public void Remove(Value value) => baseContext.Remove(value);

        /// <summary>
        /// Sets the given variable to the given block.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(BasicBlock basicBlock, TVariable var, Value value) =>
            SSABuilder.SetValue(basicBlock, var, value);

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="basicBlock">The target block.</param>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        public Value GetValue(BasicBlock basicBlock, TVariable var) =>
            SSABuilder.GetValue(basicBlock, var);

        #endregion
    }

    /// <summary>
    /// Provides <see cref="SSARewriterContext{TVariable}"/> instances.
    /// </summary>
    /// <typeparam name="TVariable">The SSA variable type.</typeparam>
    public readonly struct SSARewriterContextProvider<TVariable> :
        IRewriterContextProvider<SSARewriterContext<TVariable>, SSABuilder<TVariable>>
        where TVariable : IEquatable<TVariable>
    {
        /// <summary>
        /// Creates a new <see cref="SSARewriterContext{TVariable}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SSARewriterContext<TVariable> CreateContext(
            BasicBlock.Builder builder,
            HashSet<Value> converted,
            SSABuilder<TVariable> data) =>
            new SSARewriterContext<TVariable>(
                new RewriterContext(builder, converted),
                data);
    }

    /// <summary>
    /// A rewriter class to rewrite SSA builders.
    /// </summary>
    /// <typeparam name="TVariable">The SSA variable type.</typeparam>
    /// <typeparam name="T">The user-defined context type.</typeparam>
    public class SSARewriter<TVariable, T> :
        Rewriter<
            SSARewriterContext<TVariable>,
            SSARewriterContextProvider<TVariable>,
            SSABuilder<TVariable>,
            T>
        where TVariable : IEquatable<TVariable>
    {
        #region Nested Types

        /// <summary>
        /// Encapsulates a static rewriting step.
        /// </summary>
        public readonly new struct RewriterProcess
        {
            #region Instance

            /// <summary>
            /// Initializes a new rewriting.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RewriterProcess(
                SSARewriter<TVariable, T> rewriter,
                SSABuilder<TVariable> ssaBuilder,
                T data,
                HashSet<Value> toConvert)
            {
                Rewriter = rewriter;
                SSABuilder = ssaBuilder;
                Data = data;
                ToConvert = toConvert;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent rewriter.
            /// </summary>
            public SSARewriter<TVariable, T> Rewriter { get; }

            /// <summary>
            /// Returns the parent scope.
            /// </summary>
            public SSABuilder<TVariable> SSABuilder { get; }

            /// <summary>
            /// Returns the current data.
            /// </summary>
            public T Data { get; }

            /// <summary>
            /// Returns the set of values to convert.
            /// </summary>
            private HashSet<Value> ToConvert { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Applies the current rewriting instance.
            /// </summary>
            /// <returns>True, if the rewriter could be applied.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Rewrite() =>
                ProcessSSA(
                    SSABuilder,
                    new StaticProcessor(
                        Rewriter,
                        SSABuilder,
                        Data,
                        ToConvert));

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Processes the whole scope using the processor provided.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="ssaBuilder">The parent SSA builder..</param>
        /// <param name="processor">The processor instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool ProcessSSA<TProcessor>(
            SSABuilder<TVariable> ssaBuilder,
            TProcessor processor)
            where TProcessor : IProcessor
        {
            // Init processing
            bool applied = InitProcessing(
                ssaBuilder.MethodBuilder,
                processor,
                out var converted);

            // Process all blocks in the appropriate order
            foreach (var block in ssaBuilder.Blocks)
            {
                if (!ssaBuilder.ProcessAndSeal(block))
                    continue;

                var blockBuilder = ssaBuilder.MethodBuilder[block];
                foreach (var valueEntry in blockBuilder)
                {
                    // Check whether we have already visited the direct target
                    if (!converted.Add(valueEntry.DirectTarget))
                        continue;

                    // Move insert position to the current instruction
                    blockBuilder.SetupInsertPosition(valueEntry);

                    // Apply the processor
                    applied |= processor.Apply(
                        blockBuilder,
                        converted,
                        valueEntry.DirectTarget);
                }

                // Process terminator (if any)
                Value terminator = blockBuilder.Terminator;
                if (terminator != null && converted.Add(terminator))
                {
                    // Move insert position to the end of the block
                    blockBuilder.SetupInsertPositionToEnd();

                    // Apply the processor
                    applied |= processor.Apply(
                        blockBuilder,
                        converted,
                        terminator);
                }

                // Try to seal successor back edges
                ssaBuilder.TrySealSuccessors(block);
            }

            ssaBuilder.AssertAllSealed();
            return applied;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to rewrite the given SSA builder using the method builder provided.
        /// </summary>
        /// <param name="ssaBuilder">The parent SSA builder.</param>
        /// <param name="data">The user-defined data.</param>
        /// <param name="rewriting">The resolved rewriting functionality.</param>
        /// <returns>True, if some nodes to rewrite could have been determined.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryBeginRewrite(
            SSABuilder<TVariable> ssaBuilder,
            T data,
            out RewriterProcess rewriting)
        {
            // Determine all values to rewrite
            var toConvert = GatherNodesToConvert(ssaBuilder.Blocks, data);

            // Initialize rewriting
            rewriting = new RewriterProcess(
                this,
                ssaBuilder,
                data,
                toConvert);
            return toConvert.Count > 0;
        }

        /// <summary>
        /// Rewrites the given SSA builder on-the-fly using the method builder provided.
        /// </summary>
        /// <param name="ssaBuilder">The parent SSA builder.</param>
        /// <param name="data">The user-defined data.</param>
        /// <returns>True, if the rewriter could be applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Rewrite(SSABuilder<TVariable> ssaBuilder, T data) =>
            ProcessSSA(
                ssaBuilder,
                new DynamicProcessor(
                    this,
                    ssaBuilder,
                    data));

        #endregion
    }

    /// <summary>
    /// A rewriter class to rewrite SSA builders.
    /// </summary>
    /// <typeparam name="TVariable">The SSA variable type.</typeparam>
    public class SSARewriter<TVariable> : SSARewriter<TVariable, object>
        where TVariable : IEquatable<TVariable>
    {
        /// <summary>
        /// Tries to rewrite the given SSA builder using the method builder provided.
        /// </summary>
        /// <param name="ssaBuilder">The parent SSA builder.</param>
        /// <param name="rewriting">The resolved rewriting functionality.</param>
        /// <returns>True, if some nodes to rewrite could have been determined.</returns>
        public bool TryBeginRewrite(
            SSABuilder<TVariable> ssaBuilder,
            out RewriterProcess rewriting) =>
            TryBeginRewrite(ssaBuilder, null, out rewriting);

        /// <summary>
        /// Rewrites the given SSA builder on-the-fly using the method builder provided.
        /// </summary>
        /// <param name="ssaBuilder">The parent SSA builder.</param>
        /// <returns>True, if the rewriter could be applied.</returns>
        public bool Rewrite(SSABuilder<TVariable> ssaBuilder) =>
            Rewrite(ssaBuilder, null);
    }
}
