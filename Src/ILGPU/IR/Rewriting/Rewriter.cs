// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Rewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BlockCollection = ILGPU.IR.BasicBlockCollection<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;

namespace ILGPU.IR.Rewriting
{
    /// <summary>
    /// A rewriter predicate.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="data">The current user context.</param>
    /// <param name="value">The value to test.</param>
    /// <returns>True, if the value can be rewritten.</returns>
    public delegate bool RewritePredicate<T, TValue>(
        T data,
        TValue value);

    /// <summary>
    /// A rewriter converter that converts nodes.
    /// </summary>
    /// <typeparam name="TContext">The rewriter context type.</typeparam>
    /// <typeparam name="T">The data type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="context">The current rewriting context.</param>
    /// <param name="data">The user-defined data.</param>
    /// <param name="value">The value to rewrite.</param>
    public delegate void RewriteConverter<TContext, T, TValue>(
        TContext context,
        T data,
        TValue value)
        where TContext : struct, IRewriterContext;

    /// <summary>
    /// A rewriter class to rewrite IR nodes.
    /// </summary>
    /// <typeparam name="TContext">The rewriter context type.</typeparam>
    /// <typeparam name="TContextProvider">
    /// The provider type for new context providers.
    /// </typeparam>
    /// <typeparam name="TContextData">
    /// The context specific data to build new rewriter contexts.
    /// </typeparam>
    /// <typeparam name="T">The user-defined data type.</typeparam>
    public abstract class Rewriter<TContext, TContextProvider, TContextData, T>
        where TContext : struct, IRewriterContext
        where TContextProvider :
            struct,
            IRewriterContextProvider<TContext, TContextData>
    {
        #region Nested Types

        /// <summary>
        /// An internal converter type.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="data">The custom data context.</param>
        /// <param name="value">The value to convert.</param>
        private delegate void Converter(
            in TContext context,
            T data,
            Value value);

        /// <summary>
        /// A processor that can be applied to every value.
        /// </summary>
        protected interface IProcessor
        {
            /// <summary>
            /// Applies this processor to a particular value.
            /// </summary>
            /// <param name="blockBuilder">The current block builder.</param>
            /// <param name="converted">The set of converted values.</param>
            /// <param name="value">The current value.</param>
            bool Apply(
                BasicBlock.Builder blockBuilder,
                HashSet<Value> converted,
                Value value);
        }

        /// <summary>
        /// An adapter to use a static rewriter instance.
        /// </summary>
        protected readonly struct StaticProcessor : IProcessor
        {
            /// <summary>
            /// Initializes a new static processor.
            /// </summary>
            public StaticProcessor(
                Rewriter<TContext, TContextProvider, TContextData, T> rewriter,
                TContextData contextData,
                T data,
                HashSet<Value> toConvert)
            {
                Rewriter = rewriter;
                ContextData = contextData;
                Data = data;
                ToConvert = toConvert;
            }

            /// <summary>
            /// Returns the underlying rewriter.
            /// </summary>
            public Rewriter<
                TContext,
                TContextProvider,
                TContextData,
                T> Rewriter
            { get; }

            /// <summary>
            /// Returns the current context data.
            /// </summary>
            public TContextData ContextData { get; }

            /// <summary>
            /// Returns the current data instance.
            /// </summary>
            public T Data { get; }

            /// <summary>
            /// Returns the set of values to convert.
            /// </summary>
            public HashSet<Value> ToConvert { get; }

            /// <summary>
            /// Applies the current processing adapter.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Apply(
                BasicBlock.Builder blockBuilder,
                HashSet<Value> converted,
                Value value)
            {
                if (!ToConvert.Contains(value))
                    return false;
                TContextProvider provider = default;
                var rewriterContext = provider.CreateContext(
                    blockBuilder,
                    converted,
                    ContextData);
                Rewriter.Apply(rewriterContext, Data, value);
                ToConvert.Remove(value);
                return true;
            }
        }

        /// <summary>
        /// An adapter to use a dynamic rewriter instance.
        /// </summary>
        protected struct DynamicProcessor : IProcessor
        {
            /// <summary>
            /// Initializes a new static processor.
            /// </summary>
            public DynamicProcessor(
                Rewriter<TContext, TContextProvider, TContextData, T> rewriter,
                TContextData contextData,
                T data)
            {
                Rewriter = rewriter;
                ContextData = contextData;
                Data = data;
            }

            /// <summary>
            /// Returns the underlying rewriter.
            /// </summary>
            public Rewriter<
                TContext,
                TContextProvider,
                TContextData,
                T> Rewriter
            { get; }

            /// <summary>
            /// Returns the current context data.
            /// </summary>
            public TContextData ContextData { get; }

            /// <summary>
            /// Returns the current data instance.
            /// </summary>
            public T Data { get; }

            /// <summary>
            /// Applies the current processing adapter.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Apply(
                BasicBlock.Builder blockBuilder,
                HashSet<Value> converted,
                Value value)
            {
                if (!Rewriter.CanRewrite(Data, value))
                    return false;
                TContextProvider provider = default;
                var rewriterContext = provider.CreateContext(
                    blockBuilder,
                    converted,
                    ContextData);
                Rewriter.Apply(rewriterContext, Data, value);
                return true;
            }
        }

        /// <summary>
        /// Encapsulates a static rewriting step.
        /// </summary>
        public readonly struct RewriterProcess
        {
            #region Instance

            /// <summary>
            /// Initializes a new rewriting.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RewriterProcess(
                Rewriter<TContext, TContextProvider, TContextData, T> rewriter,
                in BlockCollection blocks,
                Method.Builder builder,
                TContextData contextData,
                T data,
                HashSet<Value> toConvert)
            {
                Rewriter = rewriter;
                Blocks = blocks;
                Builder = builder;
                ContextData = contextData;
                Data = data;
                ToConvert = toConvert;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent rewriter.
            /// </summary>
            public Rewriter<
                TContext,
                TContextProvider,
                TContextData,
                T> Rewriter
            { get; }

            /// <summary>
            /// Returns the block collection.
            /// </summary>
            public BlockCollection Blocks { get; }

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns the current context data.
            /// </summary>
            public TContextData ContextData { get; }

            /// <summary>
            /// Returns the current data instance.
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
                Process(
                    Blocks,
                    Builder,
                    new StaticProcessor(
                        Rewriter,
                        ContextData,
                        Data,
                        ToConvert));

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Initializes a processing phase by applying the processor to all parameters.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="builder">The parent method builder.</param>
        /// <param name="processor">The processor instance.</param>
        /// <param name="converted">The initializes set of converted values.</param>
        /// <returns>True, if the given processor could be applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool InitProcessing<TProcessor>(
            Method.Builder builder,
            TProcessor processor,
            out HashSet<Value> converted)
            where TProcessor : struct, IProcessor
        {
            converted = new HashSet<Value>();
            bool applied = false;

            // Apply to all parameters
            var entryBlockBuilder = builder[builder.EntryBlock];
            entryBlockBuilder.InsertPosition = 0;
            for (int i = 0, e = builder.NumParams; i < e; ++i)
            {
                // Apply the processor
                applied |= processor.Apply(
                    entryBlockBuilder,
                    converted,
                    builder[i]);
            }

            return applied;
        }

        /// <summary>
        /// Processes the whole scope using the processor provided.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="blocks">All blocks to process.</param>
        /// <param name="builder">The parent method builder.</param>
        /// <param name="processor">The processor instance.</param>
        /// <returns>True, if the given processor could be applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Process<TProcessor>(
            in BlockCollection blocks,
            Method.Builder builder,
            TProcessor processor)
            where TProcessor : struct, IProcessor
        {
            // Init processing
            bool applied = InitProcessing(
                builder,
                processor,
                out var converted);

            // Apply to all values
            foreach (var block in blocks)
            {
                var blockBuilder = builder[block];
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
                if (terminator == null || !converted.Add(terminator))
                    continue;

                // Move insert position to the end of the block
                blockBuilder.SetupInsertPositionToEnd();

                // Apply the processor
                applied |= processor.Apply(
                    blockBuilder,
                    converted,
                    terminator);
            }

            return applied;
        }

        #endregion

        #region Instance

        private readonly Func<T, Value, bool>[] predicates;
        private readonly Converter[] converters;

        /// <summary>
        /// Constructs a new rewriter instance.
        /// </summary>
        protected Rewriter()
        {
            predicates = new Func<T, Value, bool>[ValueKinds.NumValueKinds];
            converters = new Converter[ValueKinds.NumValueKinds];
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a new rewriter converter.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(RewriteConverter<TContext, T, TValue> converter)
            where TValue : Value =>
            Add((T _, TValue value) => true, converter);

        /// <summary>
        /// Adds a new rewriter converter with a specific predicate.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="predicate">The predicate to use prior to conversion.</param>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(
            RewritePredicate<T, TValue> predicate,
            RewriteConverter<TContext, T, TValue> converter)
            where TValue : Value
        {
            int valueKind = (int)ValueKinds.GetValueKind<TValue>();
            predicates[valueKind] = (userContext, value) =>
            {
                var targetValue = value as TValue;
                Debug.Assert(targetValue != null, "Invalid value conversion");
                return predicate(userContext, targetValue);
            };
            converters[valueKind] = (in TContext context, T data, Value value) =>
            {
                var targetValue = value as TValue;
                Debug.Assert(targetValue != null, "Invalid value conversion");
                converter(context, data, targetValue);
            };
        }

        /// <summary>
        /// Returns true if the given value can be rewritten.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="value">The value to test.</param>
        /// <returns>True, if the given value can be rewritten.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanRewrite(T context, Value value)
        {
            Debug.Assert(value != null, "Invalid value");
            var predicate = predicates[(int)value.ValueKind];
            return predicate?.Invoke(context, value) ?? false;
        }

        /// <summary>
        /// Applies an internal converter.
        /// </summary>
        /// <param name="context">The rewriter context.</param>
        /// <param name="data">The custom data instance.</param>
        /// <param name="value">The value to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Apply(TContext context, T data, Value value)
        {
            var converter = converters[(int)value.ValueKind];
            Debug.Assert(converter != null, "Invalid converter");
            converter(context, data, value);
        }

        /// <summary>
        /// Determines all nodes to convert.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="data">The user-defined context.</param>
        /// <returns>The set of all values to convert.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected HashSet<Value> GatherNodesToConvert(
            in BlockCollection blocks,
            T data)
        {
            // Determine all values to rewrite
            var toConvert = new HashSet<Value>();
            foreach (Value value in blocks.Values)
            {
                if (CanRewrite(data, value))
                    toConvert.Add(value);
            }
            return toConvert;
        }

        /// <summary>
        /// Tries to rewrite the given scope using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="contextData">The context data.</param>
        /// <param name="data">The user-defined data.</param>
        /// <param name="rewriting">The resolved rewriting functionality.</param>
        /// <returns>True, if some nodes to rewrite could have been determined.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TryBeginRewrite(
            in BlockCollection blocks,
            Method.Builder builder,
            TContextData contextData,
            T data,
            out RewriterProcess rewriting)
        {
            // Determine all values to rewrite
            var toConvert = GatherNodesToConvert(blocks, data);

            // Initialize rewriting
            rewriting = new RewriterProcess(
                this,
                blocks,
                builder,
                contextData,
                data,
                toConvert);
            return toConvert.Count > 0;
        }

        /// <summary>
        /// Rewrites the given scope on-the-fly using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="contextData">The context data.</param>
        /// <param name="data">The user-defined data.</param>
        /// <returns>True, if the rewriter could be applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Rewrite(
            in BlockCollection blocks,
            Method.Builder builder,
            TContextData contextData,
            T data) =>
            Process(
                blocks,
                builder,
                new DynamicProcessor(this, contextData, data));

        #endregion
    }

    /// <summary>
    /// A rewriter converter that converts nodes.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="context">The current rewriting context.</param>
    /// <param name="data">The user-defined data.</param>
    /// <param name="value">The value to rewrite.</param>
    public delegate void RewriteConverter<T, TValue>(
        RewriterContext context,
        T data,
        TValue value);

    /// <summary>
    /// A rewriter class that does not work on user-defined context instances.
    /// </summary>
    public class Rewriter<T> :
        Rewriter<RewriterContext, RewriterContextProvider<object>, object, T>
    {
        /// <summary>
        /// Adds a new rewriter converter.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(RewriteConverter<T, TValue> converter)
            where TValue : Value =>
            Add((T _, TValue value) => true, converter);

        /// <summary>
        /// Adds a new rewriter converter with a specific predicate.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="predicate">The predicate to use prior to conversion.</param>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(
            RewritePredicate<T, TValue> predicate,
            RewriteConverter<T, TValue> converter)
            where TValue : Value =>
            Add(
                predicate,
                new RewriteConverter<RewriterContext, T, TValue>(converter));

        /// <summary>
        /// Tries to rewrite the given scope using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="data">The user-defined data.</param>
        /// <param name="rewriting">The resolved rewriting functionality.</param>
        /// <returns>True, if some nodes to rewrite could have been determined.</returns>
        public bool TryBeginRewrite(
            in BlockCollection blocks,
            Method.Builder builder,
            T data,
            out RewriterProcess rewriting) =>
            TryBeginRewrite(blocks, builder, null, data, out rewriting);

        /// <summary>
        /// Rewrites the given scope on-the-fly using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="data">The user-defined data.</param>
        /// <returns>True, if the rewriter could be applied.</returns>
        public bool Rewrite(
            in BlockCollection blocks,
            Method.Builder builder,
            T data) =>
            Rewrite(blocks, builder, null, data);
    }

    /// <summary>
    /// A rewriter predicate.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The value to test.</param>
    /// <returns>True, if the value can be rewritten.</returns>
    public delegate bool RewritePredicate<in TValue>(TValue value)
        where TValue : Value;

    /// <summary>
    /// A rewriter converter that converts nodes.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="context">The current rewriting context.</param>
    /// <param name="value">The value to rewrite.</param>
    public delegate void RewriteConverter<in TValue>(
        RewriterContext context,
        TValue value)
        where TValue : Value;

    /// <summary>
    /// A rewriter class that does not work on user-defined context instances.
    /// </summary>
    public class Rewriter : Rewriter<object>
    {
        /// <summary>
        /// Adds a new rewriter converter.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(RewriteConverter<TValue> converter)
            where TValue : Value =>
            Add((TValue value) => true, converter);

        /// <summary>
        /// Adds a new rewriter converter with a specific predicate.
        /// </summary>
        /// <typeparam name="TValue">The value kind.</typeparam>
        /// <param name="predicate">The predicate to use prior to conversion.</param>
        /// <param name="converter">The rewriter converter.</param>
        public void Add<TValue>(
            RewritePredicate<TValue> predicate,
            RewriteConverter<TValue> converter)
            where TValue : Value =>
            Add<TValue>(
                (_, value) => predicate(value),
                (context, data, value) => converter(context, value));

        /// <summary>
        /// Tries to rewrite the given scope using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="rewriting">The resolved rewriting functionality.</param>
        /// <returns>True, if some nodes to rewrite could have been determined.</returns>
        public bool TryBeginRewrite(
            in BlockCollection blocks,
            Method.Builder builder,
            out RewriterProcess rewriting) =>
            TryBeginRewrite(blocks, builder, null, out rewriting);

        /// <summary>
        /// Rewrites the given scope on-the-fly using the method builder provided.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        /// <param name="builder">The current builder.</param>
        /// <returns>True, if the rewriter could be applied.</returns>
        public bool Rewrite(in BlockCollection blocks, Method.Builder builder) =>
            Rewrite(blocks, builder, null);
    }
}
