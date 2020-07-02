// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: FixPointAnalysis.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis context that manages data in the scope of a fix point analysis.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TNode">The node type.</typeparam>
    public interface IFixPointAnalysisContext<T, TNode>
        where TNode : Node
    {
        /// <summary>
        /// Returns the associated data value of the given node.
        /// </summary>
        /// <param name="node">The IR node.</param>
        /// <returns>The associated data value.</returns>
        T this[TNode node] { get; set; }
    }

    /// <summary>
    /// An abstract fix point analysis to compute static invariants.
    /// </summary>
    /// <typeparam name="TData">The underlying data type of the analysis.</typeparam>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public interface IFixPointAnalysis<TData, TNode, TDirection>
        where TNode : Node
        where TDirection : struct, IControlFlowDirection
    {
        /// <summary>
        /// Creates an initial data value for the given node.
        /// </summary>
        /// <param name="node">The source IR node.</param>
        /// <returns>The created data value.</returns>
        TData CreateData(TNode node);

        /// <summary>
        /// Updates the given value with the latest analysis insights.
        /// </summary>
        /// <typeparam name="TContext">The analysis value context.</typeparam>
        /// <param name="node">The source IR node.</param>
        /// <param name="context">The current analysis context.</param>
        /// <returns>
        /// True, if the analysis has changed the internal data values.
        /// </returns>
        bool Update<TContext>(TNode node, TContext context)
            where TContext : class, IFixPointAnalysisContext<TData, TNode>;
    }

    /// <summary>
    /// An abstract global fix point analysis to compute static invariants across
    /// different method calls.
    /// </summary>
    /// <typeparam name="TMethodData">
    /// The underlying method data type of the analysis.
    /// </typeparam>
    /// <typeparam name="TValueData">
    /// The underlying value data type of the analysis.
    /// </typeparam>
    public interface IGlobalFixPointAnalysisContext<TMethodData, TValueData> :
        IFixPointAnalysisContext<TValueData, Value>,
        IFixPointAnalysisContext<TMethodData, Method>
        where TValueData : IEquatable<TValueData>
    { }

    /// <summary>
    /// An abstract global fix point analysis to compute static invariants across
    /// different method calls.
    /// </summary>
    /// <typeparam name="TMethodData">
    /// The underlying method data type of the analysis.
    /// </typeparam>
    /// <typeparam name="TValueData">
    /// The underlying value data type of the analysis.
    /// </typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public interface IGlobalFixPointAnalysis<TMethodData, TValueData, TDirection> :
        IFixPointAnalysis<TValueData, Value, TDirection>
        where TValueData : IEquatable<TValueData>
        where TDirection : struct, IControlFlowDirection
    {
        /// <summary>
        /// Creates an initial data value for the given method.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The created method data value.</returns>
        TMethodData CreateMethodData(Method method);

        /// <summary>
        /// </summary>
        /// <typeparam name="TContext">The analysis value context.</typeparam>
        /// <param name="method">The source method.</param>
        /// <param name="arguments">The call arguments.</param>
        /// <param name="valueMapping">The current value mapping.</param>
        /// <param name="context">The current analysis context.</param>
        void UpdateMethod<TContext>(
            Method method,
            ImmutableArray<TValueData> arguments,
            Dictionary<Value, TValueData> valueMapping,
            TContext context)
            where TContext :
                class,
                IGlobalFixPointAnalysisContext<TMethodData, TValueData>;
    }

    /// <summary>
    /// Implements the actual fix-point analysis functions.
    /// </summary>
    public static class FixPointAnalysis
    {
        #region Nested Types

        /// <summary>
        /// An internal context implementation for block-based analyses.
        /// </summary>
        private abstract class BaseAnalysisContext
        {
            protected BaseAnalysisContext(BasicBlockSet onStack)
            {
                OnStack = onStack;
                Stack = new Stack<BasicBlock>();
            }

            /// <summary>
            /// Returns the current stack set.
            /// </summary>
            public BasicBlockSet OnStack { get; }

            /// <summary>
            /// Returns the current stack.
            /// </summary>
            public Stack<BasicBlock> Stack { get; }

            /// <summary>
            /// Tries to pop one block from the stack.
            /// </summary>
            /// <param name="block">The popped block (if any).</param>
            /// <returns>True, if a value could be popped from the stack.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryPop(out BasicBlock block)
            {
                block = null;
                if (Stack.Count < 1)
                    return false;
                block = Stack.Pop();
                OnStack.Remove(block);
                return true;
            }

            /// <summary>
            /// Pushes the given block into the stack.
            /// </summary>
            /// <param name="block">The block to push.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(BasicBlock block)
            {
                if (OnStack.Add(block))
                    Stack.Push(block);
            }
        }

        /// <summary>
        /// An internal context implementation for block-based analyses.
        /// </summary>
        private sealed class BlockAnalysisContext<TData> :
            BaseAnalysisContext,
            IFixPointAnalysisContext<TData, BasicBlock>
        {
            private BasicBlockMap<TData> mapping;

            public BlockAnalysisContext(
                BasicBlockMap<TData> dataMapping,
                BasicBlockSet onStack)
                : base(onStack)
            {
                mapping = dataMapping;
            }

            public TData this[BasicBlock block]
            {
                get => mapping[block];
                set => mapping[block] = value;
            }
        }

        /// <summary>
        /// An internal context implementation.
        /// </summary>
        private sealed class ValueAnalysisContext<TData> :
            BaseAnalysisContext,
            IFixPointAnalysisContext<TData, Value>
        {
            public ValueAnalysisContext(
                Dictionary<Value, TData> valueMapping,
                BasicBlockSet onStack)
                : base(onStack)
            {
                ValueMapping = valueMapping;
            }

            /// <summary>
            /// Returns the current value mapping.
            /// </summary>
            public Dictionary<Value, TData> ValueMapping { get; }

            /// <summary>
            /// Returns the data of the given node.
            /// </summary>
            /// <param name="valueNode">The value node.</param>
            /// <returns>The associated value.</returns>
            public TData this[Value valueNode]
            {
                get => ValueMapping[valueNode];
                set => ValueMapping[valueNode] = value;
            }
        }

        private readonly struct GlobalAnalysisEntry<TValue> :
            IEquatable<GlobalAnalysisEntry<TValue>>
            where TValue : IEquatable<TValue>
        {
            public GlobalAnalysisEntry(
                Method method,
                ImmutableArray<TValue> arguments)
            {
                Method = method;
                Arguments = arguments;
            }

            #region Properties

            /// <summary>
            /// Returns the current method.
            /// </summary>
            public Method Method { get; }

            /// <summary>
            /// Returns all call argument data.
            /// </summary>
            public ImmutableArray<TValue> Arguments { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given entry is equal to the current one.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>True, if the given entry is equal to the current one.</returns>
            public readonly bool Equals(GlobalAnalysisEntry<TValue> other)
            {
                if (Method != other.Method)
                    return false;
                for (int i = 0, e = Arguments.Length; i < e; ++i)
                {
                    if (!Arguments.Equals(other.Arguments[i]))
                        return false;
                }
                return true;
            }

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to the current entry.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>
            /// True, if the given object is equal to the current entry.
            /// </returns>
            public readonly override bool Equals(object obj) =>
                obj is GlobalAnalysisEntry<TValue> entry && Equals(entry);

            /// <summary>
            /// Returns the hash code of this entry.
            /// </summary>
            /// <returns>The hash code of this entry.</returns>
            public readonly override int GetHashCode()
            {
                int result = Method.GetHashCode();
                foreach (var arg in Arguments)
                    result ^= arg.GetHashCode();
                return result;
            }

            /// <summary>
            /// Returns the string representation of this entry.
            /// </summary>
            /// <returns>The string representation of this entry.</returns>
            public readonly override string ToString() =>
                $"{Method}, {string.Join(", ", Arguments)}";

            #endregion
        }

        private sealed class GlobalAnalysisContext<TMethodData, TValueData> :
            IGlobalFixPointAnalysisContext<TMethodData, TValueData>
            where TValueData : IEquatable<TValueData>
        {
            public GlobalAnalysisContext(Dictionary<Method, TMethodData> mapping)
            {
                Mapping = mapping;
                ValueMapping = new Dictionary<Value, TValueData>();
                Visited = new HashSet<GlobalAnalysisEntry<TValueData>>();
                Stack = new Stack<GlobalAnalysisEntry<TValueData>>();
            }

            /// <summary>
            /// Returns the current method mapping.
            /// </summary>
            public Dictionary<Method, TMethodData> Mapping { get; }

            /// <summary>
            /// Returns the current value mapping.
            /// </summary>
            public Dictionary<Value, TValueData> ValueMapping { get; }

            /// <summary>
            /// Returns the set of visited configurations.
            /// </summary>
            public HashSet<GlobalAnalysisEntry<TValueData>> Visited { get; }

            /// <summary>
            /// Returns the current stack.
            /// </summary>
            public Stack<GlobalAnalysisEntry<TValueData>> Stack { get; }

            /// <summary>
            /// Returns the method data of the given method.
            /// </summary>
            /// <param name="method">The method.</param>
            /// <returns>The associated method data.</returns>
            public TMethodData GetMethodData(Method method) =>
                Mapping[method];

            /// <summary>
            /// Returns the data of the given node.
            /// </summary>
            /// <param name="valueNode">The value node.</param>
            /// <returns>The associated data.</returns>
            public TValueData this[Value valueNode]
            {
                get => ValueMapping[valueNode];
                set => ValueMapping[valueNode] = value;
            }

            /// <summary>
            /// Returns the data of the given method.
            /// </summary>
            /// <param name="method">The method.</param>
            /// <returns>The associated data.</returns>
            public TMethodData this[Method method]
            {
                get => Mapping[method];
                set => Mapping[method] = value;
            }

            /// <summary>
            /// Tries to pop one method from the stack.
            /// </summary>
            /// <param name="entry">The popped method (if any).</param>
            /// <returns>True, if a method could be popped from the stack.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryPop(out GlobalAnalysisEntry<TValueData> entry)
            {
                entry = default;
                if (Stack.Count < 1)
                    return false;
                entry = Stack.Pop();
                return true;
            }

            /// <summary>
            /// Pushes the given entry into the stack.
            /// </summary>
            /// <param name="entry">The entry to push.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(in GlobalAnalysisEntry<TValueData> entry)
            {
                if (Visited.Add(entry))
                    Stack.Push(entry);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a fix point analysis working on blocks.
        /// </summary>
        /// <typeparam name="TData">The underlying analysis data type.</typeparam>
        /// <typeparam name="TDirection">The analysis control-flow direction.</typeparam>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="analysis">The control-flow analysis.</param>
        /// <param name="blocks">The list of blocks.</param>
        /// <returns>The created analysis mapping from blocks to data elements.</returns>
        public static BasicBlockMap<TData> Analyze<
            TData,
            TOrder,
            TDirection,
            TBlockDirection>(
            this IFixPointAnalysis<TData, BasicBlock, TDirection> analysis,
            in BasicBlockCollection<TOrder, TBlockDirection> blocks)
            where TData : IEquatable<TData>
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
            where TBlockDirection : struct, IControlFlowDirection
        {
            // Setup initial data mapping
            var mapping = blocks.CreateMap<TData>();
            foreach (var block in blocks)
                mapping[block] = analysis.CreateData(block);

            // Create the analysis context and perform the analysis
            var context = new BlockAnalysisContext<TData>(mapping, blocks.CreateSet());

            // Processes a single block
            void ProcessBlock(BasicBlock block)
            {
                // Check for block changes
                if (!analysis.Update(block, context))
                    return;

                // Recursive into all successors
                foreach (var nextBlock in block.GetSuccessors<TDirection>())
                    context.Push(nextBlock);
            }

            // Perform an initial pass
            foreach (var block in blocks)
                ProcessBlock(block);

            // Perform fix point iteration without a specific ordering
            while (context.TryPop(out var block))
                ProcessBlock(block);

            return mapping;
        }

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <typeparam name="TData">The underlying analysis data type.</typeparam>
        /// <typeparam name="TDirection">The analysis control-flow direction.</typeparam>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="analysis">The control-flow analysis.</param>
        /// <param name="blocks">The list of blocks.</param>
        /// <param name="valueMapping">The pre-defined map of input values.</param>
        /// <returns>The created analysis mapping from values to data elements.</returns>
        private static Dictionary<Value, TData> Analyze<
            TData,
            TDirection,
            TOrder,
            TBlockDirection>(
            this IFixPointAnalysis<TData, Value, TDirection> analysis,
            in BasicBlockCollection<TOrder, TBlockDirection> blocks,
            Dictionary<Value, TData> valueMapping)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
            where TBlockDirection : struct, IControlFlowDirection
        {
            // Setup initial data mapping
            var undefValue = blocks.Method.Context.UndefinedValue;
            valueMapping[undefValue] = analysis.CreateData(undefValue);
            foreach (var block in blocks)
            {
                foreach (Value value in block)
                {
                    if (!valueMapping.ContainsKey(value))
                        valueMapping[value] = analysis.CreateData(value);
                }
            }

            // Create the analysis context and perform the analysis
            var context = new ValueAnalysisContext<TData>(
                valueMapping,
                blocks.CreateSet());

            // Processes a single block
            void ProcessBlock(BasicBlock block)
            {
                // Check for value changes
                bool changed = false;
                foreach (Value value in block)
                    changed |= analysis.Update(value, context);
                if (!changed)
                    return;

                // Recursive into all successors
                foreach (var nextBlock in block.GetSuccessors<TDirection>())
                    context.Push(nextBlock);
            }

            foreach (var block in blocks)
                ProcessBlock(block);

            // Perform fix point iteration without a specific ordering
            while (context.TryPop(out var block))
                ProcessBlock(block);

            return valueMapping;
        }

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <typeparam name="TData">The underlying analysis data type.</typeparam>
        /// <typeparam name="TDirection">The analysis control-flow direction.</typeparam>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="analysis">The control-flow analysis.</param>
        /// <param name="blocks">The list of blocks.</param>
        /// <returns>The created analysis mapping from values to data elements.</returns>
        public static Dictionary<Value, TData> Analyze<
            TData,
            TDirection,
            TOrder,
            TBlockDirection>(
            this IFixPointAnalysis<TData, Value, TDirection> analysis,
            in BasicBlockCollection<TOrder, TBlockDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
            where TBlockDirection : struct, IControlFlowDirection =>
            analysis.Analyze(blocks, new Dictionary<Value, TData>());

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <typeparam name="TMethodData">
        /// The underlying analysis method data type.
        /// </typeparam>
        /// <typeparam name="TValueData">
        /// The underlying analysis data type.
        /// </typeparam>
        /// <typeparam name="TDirection">The analysis control-flow direction.</typeparam>
        /// <param name="analysis">The control-flow analysis.</param>
        /// <param name="rootMethod">The root method.</param>
        /// <param name="arguments">The initial parameter-argument bindings.</param>
        /// <returns>
        /// The created analysis mapping from methods to data elements.
        /// </returns>
        public static Dictionary<Method, TMethodData> AnalyzeGlobal<
            TMethodData,
            TValueData,
            TDirection>(
            this IGlobalFixPointAnalysis<TMethodData, TValueData, TDirection> analysis,
            Method rootMethod,
            ImmutableArray<TValueData> arguments)
            where TValueData : IEquatable<TValueData>
            where TDirection : struct, IControlFlowDirection
        {
            var result = new Dictionary<Method, TMethodData>();
            var context = new GlobalAnalysisContext<TMethodData, TValueData>(result);

            var current = new GlobalAnalysisEntry<TValueData>(rootMethod, arguments);
            context.Visited.Add(current);
            do
            {
                var method = current.Method;

                // Update method data
                if (!result.ContainsKey(method))
                    result.Add(method, analysis.CreateMethodData(method));

                // Wire global method data and entry arguments information
                var valueMapping = context.ValueMapping;
                valueMapping.Clear();
                for (int i = 0, e = method.NumParameters; i < e; ++i)
                {
                    if (current.Arguments.Length > i)
                        valueMapping[method.Parameters[i]] = current.Arguments[i];
                }

                // Perform an analysis step
                var valueMap = analysis.Analyze(method.Blocks, valueMapping);

                // Update all changes
                analysis.UpdateMethod(
                    current.Method,
                    current.Arguments,
                    valueMap,
                    context);

                // Register all updated call sites
                foreach (Value value in method.Blocks.Values)
                {
                    if (value is MethodCall methodCall &&
                        methodCall.Target.HasImplementation)
                    {
                        var callArguments = ImmutableArray.CreateBuilder<TValueData>(
                            methodCall.Nodes.Length);
                        foreach (var arg in methodCall.Nodes)
                            callArguments.Add(valueMap[arg]);
                        context.Push(new GlobalAnalysisEntry<TValueData>(
                            methodCall.Target,
                            callArguments.MoveToImmutable()));
                    }
                }
            }
            while (context.TryPop(out current));

            return result;
        }

        #endregion
    }
}

