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
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    public abstract class FixPointAnalysis<TData, TNode, TDirection>
        where TData : IEquatable<TData>
        where TNode : Node
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// An internal context implementation for block-based analyses.
        /// </summary>
        protected abstract class BaseAnalysisContext
        {
            /// <summary>
            /// Constructs an abstract analysis context.
            /// </summary>
            /// <param name="onStack">The block set.</param>
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

        #endregion

        #region Methods

        /// <summary>
        /// Creates an initial data value for the given node.
        /// </summary>
        /// <param name="node">The source IR node.</param>
        /// <returns>The created data value.</returns>
        protected abstract TData CreateData(TNode node);

        /// <summary>
        /// Updates the given value with the latest analysis insights.
        /// </summary>
        /// <typeparam name="TContext">The analysis value context.</typeparam>
        /// <param name="node">The source IR node.</param>
        /// <param name="context">The current analysis context.</param>
        /// <returns>
        /// True, if the analysis has changed the internal data values.
        /// </returns>
        protected abstract bool Update<TContext>(TNode node, TContext context)
            where TContext : class, IFixPointAnalysisContext<TData, TNode>;

        #endregion
    }

    /// <summary>
    /// A fix point analysis to compute static invariants across blocks.
    /// </summary>
    /// <typeparam name="TData">The underlying data type of the analysis.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public abstract class BlockFixPointAnalysis<TData, TDirection> :
        FixPointAnalysis<TData, BasicBlock, TDirection>
        where TData : IEquatable<TData>
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// An internal context implementation for block-based analyses.
        /// </summary>
        protected sealed class BlockAnalysisContext :
            BaseAnalysisContext,
            IFixPointAnalysisContext<TData, BasicBlock>
        {
            private BasicBlockMap<TData> mapping;

            /// <summary>
            /// Constructs a new block analysis context.
            /// </summary>
            /// <param name="dataMapping">The block mapping.</param>
            /// <param name="onStack">The internal block set.</param>
            public BlockAnalysisContext(
                BasicBlockMap<TData> dataMapping,
                BasicBlockSet onStack)
                : base(onStack)
            {
                mapping = dataMapping;
            }

            /// <summary>
            /// Returns the data of the given block.
            /// </summary>
            /// <param name="block">The block.</param>
            /// <returns>The associated value.</returns>
            public TData this[BasicBlock block]
            {
                get => mapping[block];
                set => mapping[block] = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a fix point analysis working on blocks.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The list of blocks.</param>
        /// <returns>The created analysis mapping from blocks to data elements.</returns>
        public BasicBlockMap<TData> Analyze<
            TOrder,
            TBlockDirection>(
            in BasicBlockCollection<TOrder, TBlockDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TBlockDirection : struct, IControlFlowDirection
        {
            // Setup initial data mapping
            var mapping = blocks.CreateMap<TData>();
            foreach (var block in blocks)
                mapping[block] = CreateData(block);

            // Create the analysis context and perform the analysis
            var context = new BlockAnalysisContext(mapping, blocks.CreateSet());

            // Processes a single block
            void ProcessBlock(BasicBlock block)
            {
                // Check for block changes
                if (!Update(block, context))
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

        #endregion
    }


    /// <summary>
    /// A fix point analysis to compute static invariants across blocks.
    /// </summary>
    /// <typeparam name="T">The underlying data type of the analysis.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public abstract class ValueFixPointAnalysis<T, TDirection> :
        FixPointAnalysis<AnalysisValue<T>, Value, TDirection>
        where T : struct, IEquatable<T>
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// An internal context implementation.
        /// </summary>
        protected sealed class ValueAnalysisContext :
            BaseAnalysisContext,
            IFixPointAnalysisContext<AnalysisValue<T>, Value>,
            IAnalysisValueContext<T>
        {
            /// <summary>
            /// Returns the current value mapping.
            /// </summary>
            private readonly AnalysisValueMapping<T> mapping;

            /// <summary>
            /// Constructs a new analysis context.
            /// </summary>
            /// <param name="valueMapping">The parent value mapping.</param>
            /// <param name="onStack">The internal block set.</param>
            public ValueAnalysisContext(
                AnalysisValueMapping<T> valueMapping,
                BasicBlockSet onStack)
                : base(onStack)
            {
                mapping = valueMapping;
            }

            /// <summary>
            /// Returns the data of the given node.
            /// </summary>
            /// <param name="valueNode">The value node.</param>
            /// <returns>The associated value.</returns>
            public AnalysisValue<T> this[Value valueNode]
            {
                get => mapping[valueNode];
                set => mapping[valueNode] = value;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new fix point analysis.
        /// </summary>
        /// <param name="defaultValue">
        /// The default analysis value for generic IR nodes.
        /// </param>
        protected ValueFixPointAnalysis(T defaultValue)
        {
            DefaultValue = defaultValue;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the default analysis value for generic IR nodes.
        /// </summary>
        public T DefaultValue { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the given value with the latest analysis insights.
        /// </summary>
        /// <typeparam name="TContext">The analysis value context.</typeparam>
        /// <param name="node">The source IR node.</param>
        /// <param name="context">The current analysis context.</param>
        /// <returns>
        /// True, if the analysis has changed the internal data values.
        /// </returns>
        protected override bool Update<TContext>(Value node, TContext context)
        {
            var oldValue = context[node];
            var newValue = Merge(oldValue, node, context as ValueAnalysisContext);
            context[node] = newValue;
            return oldValue != newValue;
        }

        /// <summary>
        /// Executes a fix point analysis working on values on the given method.
        /// </summary>
        /// <typeparam name="TContext">
        /// The analysis value context to use for each parameter.
        /// </typeparam>
        /// <param name="method">The current method.</param>
        /// <param name="context">The initial value context.</param>
        /// <returns>The created analysis mapping from values to data elements.</returns>
        public (AnalysisValue<T> ReturnValue, AnalysisValueMapping<T> Values)
            AnalyzeMethod<TContext>(
            Method method,
            TContext context)
            where TContext : IAnalysisValueSourceContext<T>
        {
            // Init the value mapping for each parameter.
            var valueMapping = new Dictionary<
                Value,
                AnalysisValue<T>>(method.NumParameters);
            foreach (var param in method.Parameters)
                valueMapping[param] = context[param];

            // Perform an analysis step
            return Analyze(
                method.Blocks,
                new AnalysisValueMapping<T>(valueMapping),
                AnalysisReturnValueMapping.Create<T>());
        }

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The list of blocks.</param>
        /// <returns>The created analysis mapping from values to data elements.</returns>
        public (AnalysisValue<T> ReturnValue, AnalysisValueMapping<T> Values) Analyze<
            TOrder,
            TBlockDirection>(
            in BasicBlockCollection<TOrder, TBlockDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TBlockDirection : struct, IControlFlowDirection =>
            Analyze(
                blocks,
                AnalysisValueMapping.Create<T>(),
                AnalysisReturnValueMapping.Create<T>());

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <typeparam name="TOrder">The current order.</typeparam>
        /// <typeparam name="TBlockDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The list of blocks.</param>
        /// <param name="valueMapping">The pre-defined map of input values.</param>
        /// <param name="returnMapping">
        /// The pre-defined map of method return values.
        /// </param>
        /// <returns>The created analysis mapping from values to data elements.</returns>
        public (AnalysisValue<T> ReturnValue, AnalysisValueMapping<T> Values) Analyze<
            TOrder,
            TBlockDirection>(
            in BasicBlockCollection<TOrder, TBlockDirection> blocks,
            AnalysisValueMapping<T> valueMapping,
            AnalysisReturnValueMapping<T> returnMapping)
            where TOrder : struct, ITraversalOrder
            where TBlockDirection : struct, IControlFlowDirection
        {
            // Setup initial data mapping
            var undefValue = blocks.Method.Context.UndefinedValue;
            valueMapping[undefValue] = CreateData(undefValue);
            returnMapping[blocks.Method] = CreateData(blocks.Method);
            foreach (var block in blocks)
            {
                // Register all values
                foreach (Value value in block)
                {
                    if (!valueMapping.ContainsKey(value))
                        valueMapping[value] = CreateData(value);
                }
            }

            // Create the analysis context and perform the analysis
            var context = new ValueAnalysisContext(
                valueMapping,
                blocks.CreateSet());

            // Processes a single block
            void ProcessBlock(BasicBlock block)
            {
                // Check for value changes
                bool changed = false;
                foreach (Value value in block)
                    changed |= Update(value, context);
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

            return (returnMapping[blocks.Method], valueMapping);
        }

        /// <summary>
        /// Tries to provide an analysis value for the given type.
        /// </summary>
        /// <param name="typeNode">The type node.</param>
        /// <returns>The provided analysis value (if any).</returns>
        protected abstract AnalysisValue<T>? TryProvide(TypeNode typeNode);

        /// <summary>
        /// Tries to merge the given IR value.
        /// </summary>
        /// <typeparam name="TContext">The current value context.</typeparam>
        /// <param name="value">The IR value.</param>
        /// <param name="context">The current analysis value context.</param>
        /// <returns>A merged value in the case of a successful merge.</returns>
        protected abstract AnalysisValue<T>? TryMerge<TContext>(
            Value value,
            TContext context)
            where TContext : IAnalysisValueContext<T>;

        /// <summary>
        /// Merges the given intermediate values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The merged value.</returns>
        protected abstract T Merge(T first, T second);

        /// <summary>
        /// Merges the given intermediate values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The merged value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected AnalysisValue<T> Merge(
            AnalysisValue<T> first,
            AnalysisValue<T> second)
        {
            Debug.Assert(first.NumFields == second.NumFields);
            var childData = new T[first.NumFields];
            for (int i = 0, e = first.NumFields; i < e; ++i)
                childData[i] = Merge(first[i], second[i]);
            return new AnalysisValue<T>(
                Merge(first.Data, second.Data),
                childData);
        }

        /// <summary>
        /// Creates a new analysis value for the given type node.
        /// </summary>
        /// <param name="data">The data value.</param>
        /// <param name="type">The type node.</param>
        /// <returns>The created analysis value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static AnalysisValue<T> Create(T data, TypeNode type)
        {
            if (type is StructureType structureType)
            {
                var childData = new T[structureType.NumFields];
                for (int i = 0, e = childData.Length; i < e; ++i)
                    childData[i] = data;
                return new AnalysisValue<T>(data, childData);
            }
            return new AnalysisValue<T>(data);
        }

        /// <summary>
        /// Creates an initial analysis value.
        /// </summary>
        /// <param name="type">The type node.</param>
        /// <returns>The created analysis value.</returns>
        protected AnalysisValue<T> Create(TypeNode type)
        {
            if (type is StructureType structureType)
            {
                var childData = new T[structureType.NumFields];
                for (int i = 0, e = childData.Length; i < e; ++i)
                    childData[i] = Create(structureType[i]).Data;
                T data = childData[0];
                for (int i = 1, e = childData.Length; i < e; ++i)
                    data = Merge(data, childData[i]);
                return new AnalysisValue<T>(data, childData);
            }
            return TryProvide(type) ?? Create(DefaultValue, type);
        }

        /// <summary>
        /// Merges the given IR value into the current analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="value">The IR value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> Merge<TContext>(
            in AnalysisValue<T> source,
            Value value,
            TContext context)
            where TContext : IAnalysisValueContext<T> =>
            value switch
            {
                GetField getField => GetField(source, getField, context),
                SetField setField => SetField(source, setField, context),
                StructureValue structureValue =>
                    StructureValue(source, structureValue, context),
                PhiValue phiValue => PhiValue(source, phiValue, context),
                _ => TryMerge(value, context) ??
                    GenericValue(source, value, context),
            };

        /// <summary>
        /// Merges a <see cref="Values.GetField"/> IR value into this analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="getField">The IR value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> GetField<TContext>(
            in AnalysisValue<T> source,
            GetField getField,
            TContext context)
            where TContext : IAnalysisValueContext<T>
        {
            var sourceValue = context[getField.ObjectValue];
            var fieldSpan = getField.FieldSpan;
            if (!fieldSpan.HasSpan)
            {
                return Create(
                    Merge(
                        source.Data,
                        sourceValue[fieldSpan.Index]),
                    getField.Type);
            }

            var newChildData = new T[fieldSpan.Span];
            var newData = source.Data;
            for (int i = 1, e = newChildData.Length; i < e; ++i)
            {
                newChildData[i] = sourceValue[fieldSpan.Index + i];
                newData = Merge(newData, newChildData[i]);
            }

            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.SetField"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="setField">The IR value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> SetField<TContext>(
            in AnalysisValue<T> source,
            SetField setField,
            TContext context)
            where TContext : IAnalysisValueContext<T>
        {
            var newChildData = source.CloneChildData();

            var nestedValue = context[setField.Value].Data;
            var fieldSpan = setField.FieldSpan;
            for (int i = 0; i < fieldSpan.Span; ++i)
                newChildData[fieldSpan.Index + i] = nestedValue;

            return new AnalysisValue<T>(
                Merge(source.Data, nestedValue),
                newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.StructureValue"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="structureValue">The IR structure value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> StructureValue<TContext>(
            in AnalysisValue<T> source,
            StructureValue structureValue,
            TContext context)
            where TContext : IAnalysisValueContext<T>
        {
            var newChildData = new T[source.NumFields];
            var newData = source.Data;
            for (int i = 0, e = source.NumFields; i < e; ++i)
            {
                var childDataEntry = context[structureValue[i]].Data;
                newData = Merge(newData, childDataEntry);
                newChildData[i] = childDataEntry;
            }
            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.PhiValue"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="phi">The IR phi value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> PhiValue<TContext>(
            in AnalysisValue<T> source,
            PhiValue phi,
            TContext context)
            where TContext : IAnalysisValueContext<T>
        {
            if (source.NumFields < 1)
                return GenericValue(source, phi, context);

            var newChildData = source.CloneChildData();
            var newData = source.Data;
            foreach (Value node in phi.Nodes)
            {
                var childDataEntry = context[node];
                for (int i = 0, e = source.NumFields; i < e; ++i)
                {
                    newData = Merge(newData, childDataEntry.Data);
                    newChildData[i] = Merge(newChildData[i], childDataEntry[i]);
                }
            }
            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a generic IR value into this analysis value.
        /// </summary>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="source">The source value to merge.</param>
        /// <param name="value">The IR value to merge with.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        protected AnalysisValue<T> GenericValue<TContext>(
            AnalysisValue<T> source,
            Value value,
            TContext context)
            where TContext : IAnalysisValueContext<T>
        {
            var newData = source.Data;
            foreach (Value node in value.Nodes)
                newData = Merge(newData, context[node].Data);
            return Create(newData, value.Type);
        }

        #endregion
    }

    /// <summary>
    /// An abstract global fix point analysis to compute static invariants across
    /// different method calls.
    /// </summary>
    /// <typeparam name="TMethodData">
    /// The underlying method data type of the analysis.
    /// </typeparam>
    /// <typeparam name="T">
    /// The underlying value data type of the analysis.
    /// </typeparam>
    public interface IGlobalFixPointAnalysisContext<TMethodData, T> :
        IFixPointAnalysisContext<AnalysisValue<T>, Value>,
        IFixPointAnalysisContext<TMethodData, Method>
        where T : IEquatable<T>
    { }

    /// <summary>
    /// An abstract global fix point analysis to compute static invariants across
    /// different method calls.
    /// </summary>
    /// <typeparam name="TMethodData">
    /// The underlying method data type of the analysis.
    /// </typeparam>
    /// <typeparam name="T">
    /// The underlying value data type of the analysis.
    /// </typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public abstract class GlobalFixPointAnalysis<TMethodData, T, TDirection> :
        ValueFixPointAnalysis<T, TDirection>
        where T : struct, IEquatable<T>
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Represents an internal analysis entry.
        /// </summary>
        /// <typeparam name="TValue">The value type to track.</typeparam>
        protected readonly struct GlobalAnalysisEntry<TValue> :
            IEquatable<GlobalAnalysisEntry<TValue>>
            where TValue : IEquatable<TValue>
        {
            /// <summary>
            /// Constructs an internal analysis entry.
            /// </summary>
            /// <param name="method">The associated method.</param>
            /// <param name="arguments">The argument bindings.</param>
            public GlobalAnalysisEntry(
                Method method,
                ImmutableArray<AnalysisValue<TValue>> arguments)
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
            public ImmutableArray<AnalysisValue<TValue>> Arguments { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given entry is equal to the current one.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>True, if the given entry is equal to the current one.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Equals(GlobalAnalysisEntry<TValue> other)
            {
                if (Method != other.Method)
                    return false;
                for (int i = 0, e = Arguments.Length; i < e; ++i)
                {
                    if (!Arguments[i].Equals(other.Arguments[i]))
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

            /// <summary>
            /// Returns true if the left and right entries are the same.
            /// </summary>
            /// <param name="left">The left entry.</param>
            /// <param name="right">The right entry.</param>
            /// <returns>True, if the left and right entries are the same.</returns>
            public static bool operator ==(
                GlobalAnalysisEntry<TValue> left,
                GlobalAnalysisEntry<TValue> right) =>
                left.Equals(right);

            /// <summary>
            /// Returns true if the left and right entries are not the same.
            /// </summary>
            /// <param name="left">The left entry.</param>
            /// <param name="right">The right entry.</param>
            /// <returns>True, if the left and right entries are not the same.</returns>
            public static bool operator !=(
                GlobalAnalysisEntry<TValue> left,
                GlobalAnalysisEntry<TValue> right) =>
                !(left == right);

            #endregion
        }

        /// <summary>
        /// Implements a global analysis context.
        /// </summary>
        protected sealed class GlobalAnalysisContext :
            IGlobalFixPointAnalysisContext<TMethodData, T>
        {
            /// <summary>
            /// Constructs a new global analysis context.
            /// </summary>
            /// <param name="mapping">The basic data mapping.</param>
            public GlobalAnalysisContext(Dictionary<Method, TMethodData> mapping)
            {
                Mapping = mapping;
                ValueMapping = new Dictionary<Value, AnalysisValue<T>>();
                Visited = new HashSet<GlobalAnalysisEntry<T>>();
                Stack = new Stack<GlobalAnalysisEntry<T>>();
            }

            /// <summary>
            /// Returns the current method mapping.
            /// </summary>
            public Dictionary<Method, TMethodData> Mapping { get; }

            /// <summary>
            /// Returns the current value mapping.
            /// </summary>
            public Dictionary<Value, AnalysisValue<T>> ValueMapping { get; }

            /// <summary>
            /// Returns the set of visited configurations.
            /// </summary>
            public HashSet<GlobalAnalysisEntry<T>> Visited { get; }

            /// <summary>
            /// Returns the current stack.
            /// </summary>
            public Stack<GlobalAnalysisEntry<T>> Stack { get; }

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
            public AnalysisValue<T> this[Value valueNode]
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
            public bool TryPop(out GlobalAnalysisEntry<T> entry)
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
            public void Push(in GlobalAnalysisEntry<T> entry)
            {
                if (Visited.Add(entry))
                    Stack.Push(entry);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new global fix point analysis.
        /// </summary>
        /// <param name="defaultValue">
        /// The default analysis value for generic IR nodes.
        /// </param>
        protected GlobalFixPointAnalysis(T defaultValue)
            : base(defaultValue)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates an initial data value for the given method.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The created method data value.</returns>
        protected abstract TMethodData CreateMethodData(Method method);

        /// <summary>
        /// </summary>
        /// <typeparam name="TContext">The analysis value context.</typeparam>
        /// <param name="method">The source method.</param>
        /// <param name="arguments">The call arguments.</param>
        /// <param name="valueMapping">The current value mapping.</param>
        /// <param name="context">The current analysis context.</param>
        protected abstract void UpdateMethod<TContext>(
            Method method,
            ImmutableArray<AnalysisValue<T>> arguments,
            AnalysisValueMapping<T> valueMapping,
            TContext context)
            where TContext :
                class,
                IGlobalFixPointAnalysisContext<TMethodData, T>;

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <param name="rootMethod">The root method.</param>
        /// <param name="arguments">The initial parameter-argument bindings.</param>
        /// <returns>
        /// The created analysis mapping from methods to data elements.
        /// </returns>
        public Dictionary<Method, TMethodData> AnalyzeGlobal(
            Method rootMethod,
            ImmutableArray<AnalysisValue<T>> arguments)
        {
            var result = new Dictionary<Method, TMethodData>();
            var context = new GlobalAnalysisContext(result);

            var current = new GlobalAnalysisEntry<T>(rootMethod, arguments);
            context.Visited.Add(current);
            do
            {
                var method = current.Method;

                // Update method data
                if (!result.ContainsKey(method))
                    result.Add(method, CreateMethodData(method));

                // Wire global method data and entry arguments information
                var valueMapping = context.ValueMapping;
                valueMapping.Clear();
                for (int i = 0, e = method.NumParameters; i < e; ++i)
                {
                    if (current.Arguments.Length > i)
                        valueMapping[method.Parameters[i]] = current.Arguments[i];
                }

                // Perform an analysis step
                var valueMap = Analyze(
                    method.Blocks,
                    new AnalysisValueMapping<T>(valueMapping));

                // Update all changes
                UpdateMethod(
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
                        var callArguments = ImmutableArray.CreateBuilder<
                            AnalysisValue<T>>(
                            methodCall.Nodes.Length);
                        foreach (var arg in methodCall.Nodes)
                            callArguments.Add(valueMap[arg]);
                        context.Push(new GlobalAnalysisEntry<T>(
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

    /// <summary>
    /// An abstract global fix point analysis to compute static invariants across
    /// different method calls.
    /// </summary>
    /// <typeparam name="T">
    /// The underlying value data type of the analysis.
    /// </typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public abstract class GlobalFixPointAnalysis<T, TDirection> :
        GlobalFixPointAnalysis<
            AnalysisValueMapping<T>,
            T,
            TDirection>
        where T : struct, IEquatable<T>
        where TDirection : struct, IControlFlowDirection
    {
        #region Instance

        /// <summary>
        /// Constructs a new global fix point analysis.
        /// </summary>
        /// <param name="defaultValue">
        /// The default analysis value for generic IR nodes.
        /// </param>
        /// <param name="initialValue">
        /// The initial analysis value for entry-point IR nodes.
        /// </param>
        protected GlobalFixPointAnalysis(T defaultValue, T initialValue)
            : base(defaultValue)
        {
            InitialValue = initialValue;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the initial analysis value.
        /// </summary>
        public T InitialValue { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates an initial data value for the given method.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The created method data value.</returns>
        protected override AnalysisValueMapping<T>
            CreateMethodData(Method method) =>
            AnalysisValueMapping.Create<T>();

        /// <summary>
        /// Merges previous value bindings with the latest argument value updates.
        /// </summary>
        protected override void UpdateMethod<TContext>(
            Method method,
            ImmutableArray<AnalysisValue<T>> arguments,
            AnalysisValueMapping<T> valueMapping,
            TContext context)
        {
            var data = context[method];
            foreach (var entry in valueMapping)
            {
                var value = entry.Value;
                if (data.TryGetValue(entry.Key, out var oldValue))
                    value = Merge(oldValue, value);
                data[entry.Key] = value;
            }
        }

        /// <summary>
        /// Executes a fix point analysis working on values.
        /// </summary>
        /// <param name="rootMethod">The root method.</param>
        /// <returns>
        /// The created analysis mapping from methods to data elements.
        /// </returns>
        public Dictionary<Method, AnalysisValueMapping<T>> AnalyzeGlobal(
            Method rootMethod)
        {
            var parameters = ImmutableArray.CreateBuilder<AnalysisValue<T>>(
                rootMethod.NumParameters);

            foreach (var param in rootMethod.Parameters)
                parameters.Add(Create(InitialValue, param.Type));

            return AnalyzeGlobal(rootMethod, parameters.MoveToImmutable());
        }

        #endregion
    }
}

