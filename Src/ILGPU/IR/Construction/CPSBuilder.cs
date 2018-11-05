// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CPSBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents the abstract interface for all
    /// <see cref="CPSBuilder{TNode, TEnumerator, TVariable}"/> nodes.
    /// </summary>
    /// <typeparam name="TNode">The actual type of the child node (for performance reasons).</typeparam>
    /// <typeparam name="TEnumerator">The enumerator type (for performance reasons).</typeparam>
    public interface ICPSBuilderNode<TNode, TEnumerator> : ICFGNode
        where TNode : class
        where TEnumerator : IEnumerator<TNode>
    {
        /// <summary>
        /// Returns the successors of this node.
        /// </summary>
        TEnumerator GetSuccessorEnumerator();
    }

    /// <summary>
    /// An abstract argument callback closure to add arguments to a call.
    /// </summary>
    public interface ICPSBuilderFunctionCallArgumentCallback
    {
        /// <summary>
        /// Adds the given argument to the call argument list.
        /// </summary>
        /// <param name="value">The argument to add (can be null to add a placeholder value).</param>
        void AddArgument(Value value);
    }

    /// <summary>
    /// Represents a function provider used to construct empty "basic blocks"
    /// during the construction process.
    /// </summary>
    /// <typeparam name="TNode"></typeparam>
    public interface ICPSBuilderFunctionProvider<TNode>
        where TNode : class
    {
        /// <summary>
        /// Resolves a function builder for the given child nodes.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="childNode">The source node.</param>
        /// <returns>The resolved function builder.</returns>
        FunctionBuilder GetFunctionBuilder(IRBuilder builder, TNode childNode);

        /// <summary>
        /// Resolves a (potentially) mapped function call argument.
        /// </summary>
        /// <param name="attachedFunction">The source function.</param>
        /// <param name="targetBuilder">The target function builder.</param>
        /// <param name="currentCall">The current call.</param>
        /// <param name="argumentIdx">The current argument index.</param>
        /// <param name="callback">The target value callback.</param>
        void ResolveFunctionCallArgument<TCallback>(
            FunctionValue attachedFunction,
            FunctionBuilder targetBuilder,
            FunctionCall currentCall,
            int argumentIdx,
            ref TCallback callback)
            where TCallback : ICPSBuilderFunctionCallArgumentCallback;
    }

    /// <summary>
    /// The abstract interface for a CPS builder.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TVariable">The variable type.</typeparam>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes",
        Justification = "A generic class that avoids unnecessary boxing")]
    public interface ICPSBuilder<TNode, TVariable>
        where TVariable : IEquatable<TVariable>
    {
        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(TNode node, TVariable var, Value value);

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        Value GetValue(TNode node, TVariable var);

        /// <summary>
        /// Sets the terminator value.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="terminator">The terminator to set.</param>
        void SetTerminator(TNode node, Value terminator);
    }

    /// <summary>
    /// Constructs IR nodes that are in CPS form.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TEnumerator">The node enumerator type.</typeparam>
    /// <typeparam name="TVariable">The variable type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes",
        Justification = "A generic class that avoids unnecessary boxing")]
    public sealed class CPSBuilder<TNode, TEnumerator, TVariable> : ICPSBuilder<TNode, TVariable>
        where TNode : class, ICPSBuilderNode<TNode, TEnumerator>
        where TEnumerator : IEnumerator<TNode>
        where TVariable : IEquatable<TVariable>
    {
        #region Nested Types

        /// <summary>
        /// A successor or predecessor enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<TNode>
        {
            private readonly HashSet<BasicBlock> set;
            private HashSet<BasicBlock>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="values">The values to enumerate.</param>
            internal Enumerator(HashSet<BasicBlock> values)
            {
                set = values;
                enumerator = default;
                Reset();
            }

            /// <summary>
            /// Returns the current value.
            /// </summary>
            public TNode Current => enumerator.Current.Node;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose()
            {
                enumerator.Dispose();
            }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                return enumerator.MoveNext();
            }

            /// <summary cref="IEnumerator.Reset"/>
            public void Reset()
            {
                enumerator = set.GetEnumerator();
            }
        }

        /// <summary>
        /// Provides marker values.
        /// </summary>
        internal ref struct MarkerProvider
        {
            /// <summary>
            /// Constructs a new marker provider.
            /// </summary>
            /// <param name="markerValue">The current marker value.</param>
            public MarkerProvider(int markerValue)
            {
                MarkerValue = markerValue;
            }

            /// <summary>
            /// Returns the current marker value.
            /// </summary>
            public int MarkerValue { get; private set; }

            /// <summary>
            /// Creates a new marker value.
            /// </summary>
            /// <returns>The created marker value.</returns>
            public int CreateMarker() =>
                ++MarkerValue;

            /// <summary>
            /// Applies the internal marker value to the given target.
            /// </summary>
            /// <param name="targetMarkerValue">The target marker value reference.</param>
            public void Apply(ref int targetMarkerValue)
            {
                targetMarkerValue = MarkerValue;
            }
        }

        /// <summary>
        /// Represents a basic block during cps construction.
        /// </summary>
        internal sealed class BasicBlock
        {
            #region Nested Types

            /// <summary>
            /// Represents an incomplete phi parameter that has to be
            /// completed by adding its required operands later on.
            /// </summary>
            private readonly struct IncompletePhi
            {
                /// <summary>
                /// Constructs an incomplete phi.
                /// </summary>
                /// <param name="variableRef">The referenced variable.</param>
                /// <param name="phi">The phi function.</param>
                /// <param name="phiIndex">The index of the phi argument.</param>
                public IncompletePhi(
                    TVariable variableRef,
                    Parameter phi,
                    int phiIndex)
                {
                    VariableRef = variableRef;
                    PhiParam = phi;
                    PhiIndex = phiIndex;
                }

                /// <summary>
                /// Returns the associated variable ref.
                /// </summary>
                public TVariable VariableRef { get; }

                /// <summary>
                /// Returns the associated phi parameter.
                /// </summary>
                public Parameter PhiParam { get; }

                /// <summary>
                /// Returns the index of the phi argument;
                /// </summary>
                public int PhiIndex { get; }

                /// <summary>
                /// Returns the type of the phi node.
                /// </summary>
                public TypeNode PhiType => PhiParam.Type;
            }

            #endregion

            #region Instance

            /// <summary>
            /// Represents the internal marker value.
            /// </summary>
            private int markerValue = 0;

            /// <summary>
            /// Value cache for SSA GetValue and SetValue functionality.
            /// </summary>
            private readonly Dictionary<TVariable, Value> values = new Dictionary<TVariable, Value>();

            /// <summary>
            /// Set of predecessors.
            /// </summary>
            private readonly HashSet<BasicBlock> predecessors = new HashSet<BasicBlock>();

            /// <summary>
            /// Set of successors.
            /// </summary>
            private readonly HashSet<BasicBlock> successors = new HashSet<BasicBlock>();

            /// <summary>
            /// The ordered list of successors.
            /// </summary>
            private readonly List<BasicBlock> successorList = new List<BasicBlock>();

            /// <summary>
            /// Container for incomplete "phis" that have to be wired during block sealing.
            /// </summary>
            private readonly Dictionary<TVariable, IncompletePhi> incompletePhis =
                new Dictionary<TVariable, IncompletePhi>();

            /// <summary>
            /// The target arguments.
            /// </summary>
            private Value[] arguments = new Value[0];

            /// <summary>
            /// Constructs a new basic block.
            /// </summary>
            /// <param name="node">The associated node.</param>
            /// <param name="builder">The associated IR builder.</param>
            /// <param name="functionBuilder">The associated function builder.</param>
            internal BasicBlock(
                TNode node,
                IRBuilder builder,
                FunctionBuilder functionBuilder)
            {
                Debug.Assert(functionBuilder != null, "Invalid function builder");
                Node = node;
                Builder = builder;
                FunctionBuilder = functionBuilder;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated node.
            /// </summary>
            public TNode Node { get; }

            /// <summary>
            /// Returns the associated IR builder.
            /// </summary>
            public IRBuilder Builder { get; }

            /// <summary>
            /// Returns the associated function builder.
            /// </summary>
            public FunctionBuilder FunctionBuilder { get; }

            /// <summary>
            /// Returns the associated function.
            /// </summary>
            public FunctionValue FunctionValue => FunctionBuilder.FunctionValue;

            /// <summary>
            /// Returns True iff this block is sealed.
            /// </summary>
            public bool IsSealed { get; private set; }

            /// <summary>
            /// Returns true iff this block can be sealed.
            /// </summary>
            public bool CanSeal
            {
                get
                {
                    if (IsSealed)
                        return false;
                    foreach (var predecessor in predecessors)
                    {
                        if (!predecessor.IsProcessed &&
                            !predecessor.IsSealed)
                            return false;
                    }
                    return true;
                }
            }

            /// <summary>
            /// Returns true iff this block has been processed.
            /// </summary>
            public bool IsProcessed { get; set; }

            /// <summary>
            /// Gets or sets the block terminator condition.
            /// </summary>
            public Value Terminator { get; set; }

            /// <summary>
            /// Returns the number of predecessors.
            /// </summary>
            public int NumPredecessors => predecessors.Count;

            /// <summary>
            /// Returns the number of successors.
            /// </summary>
            public int NumSuccessors => successors.Count;

            /// <summary>
            /// Returns a predecessor enumerator.
            /// </summary>
            public Enumerator Predecessors => new Enumerator(predecessors);

            /// <summary>
            /// Returns a successor enumerator.
            /// </summary>
            public Enumerator Successors => new Enumerator(successors);

            /// <summary>
            /// Returns true iff the current block is a call target.
            /// </summary>
            public bool IsCallTarget { get; private set; }

            #endregion

            #region Methods

            /// <summary>
            /// Marks the current block with the new marker value.
            /// </summary>
            /// <param name="newMarker">The new value to apply.</param>
            /// <returns>
            /// True, iff the old marker was not equal to the new marker
            /// (the block was not marked with the new marker value).
            /// </returns>
            public bool Mark(int newMarker)
            {
                return Interlocked.Exchange(ref markerValue, newMarker) != newMarker;
            }

            /// <summary>
            /// Adds the given block as successor.
            /// </summary>
            /// <param name="successor">The successor to add.</param>
            internal void AddSuccessor(BasicBlock successor)
            {
                Debug.Assert(successor != null, "Invalid successor");
                if (successor.IsSealed)
                    throw new InvalidOperationException("Cannot add a predecessor to a sealed node");
                if (successors.Add(successor))
                    successorList.Add(successor);
                successor.predecessors.Add(this);
            }

            /// <summary>
            /// Sets a phi-param argument.
            /// </summary>
            /// <param name="index">The parameter index.</param>
            /// <param name="argument">The argument value.</param>
            public void SetArgument(int index, Value argument)
            {
                Debug.Assert(index >= 0, "Invalid index");
                Debug.Assert(argument != null, "Invalid argument");
                if (arguments == null || arguments.Length < index + 1)
                    Array.Resize(ref arguments, index + 1);
                arguments[index] = argument;
            }

            /// <summary>
            /// Sets the given variable to the given value.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="value">The value to set.</param>
            public void SetValue(TVariable var, Value value)
            {
                values[var] = value;
            }

            /// <summary>
            /// Returns the value of the given variable.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="markerProvider">A provider of new marker values.</param>
            /// <returns>The value of the given variable.</returns>
            public Value GetValue(TVariable var, ref MarkerProvider markerProvider)
            {
                if (values.TryGetValue(var, out Value value))
                    return value;
                return GetValueRecursive(var, ref markerProvider);
            }

            /// <summary>
            /// Removes the value of the given variable.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            public void RemoveValue(TVariable var)
            {
                values.Remove(var);
            }

            /// <summary>
            /// Peeks a value recursively. This method only retrieves a value
            /// from a predecessor but does not build any phi nodes.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="marker">The current marker to break cycles.</param>
            /// <returns></returns>
            private Value PeekValue(TVariable var, int marker)
            {
                if (!IsProcessed || !Mark(marker))
                    return null;
                if (values.TryGetValue(var, out Value value))
                    return value;
                foreach (var predecessor in predecessors)
                {
                    Value result;
                    if ((result = predecessor.PeekValue(var, marker))!= null)
                        return result;
                }
                return null;
            }

            /// <summary>
            /// Returns the value of the given variable by asking the predecessors.
            /// This method recursively constructs required phi nodes to break cycles.
            /// </summary>
            /// <param name="var">The variable reference.</param>
            /// <param name="markerProvider">A provider of new marker values.</param>
            /// <returns>The value of the given variable.</returns>
            private Value GetValueRecursive(TVariable var, ref MarkerProvider markerProvider)
            {
                Debug.Assert(predecessors.Count > 0);
                Value value;
                var predecessorEnumerator = predecessors.GetEnumerator();
                if (predecessors.Count == 1 && IsSealed)
                {
                    predecessorEnumerator.MoveNext();
                    value = predecessorEnumerator.Current.GetValue(var, ref markerProvider);
                }
                else
                {
                    value = PeekValue(var, markerProvider.CreateMarker());
                    Debug.Assert(value != null, "Invalid processed predecessors");

                    if (IsCallTarget)
                        throw new InvalidOperationException($"Cannot add a phi argument to the call target '{this}'");

                    // Insert the actual phi param
                    var phiParam = FunctionBuilder.AddParameter(value.Type, "phi");

                    var incompletePhi = new IncompletePhi(
                        var,
                        phiParam,
                        FunctionBuilder.NumParams - 1);

                    value = phiParam;
                    if (IsSealed)
                    {
                        SetValue(var, value);
                        SetupPhiArguments(incompletePhi, ref markerProvider);
                    }
                    else
                        incompletePhis[var] = incompletePhi;
                }
                SetValue(var, value);
                return value;
            }

            /// <summary>
            /// Setups phi arguments for the given variable reference and the given
            /// phi parameter. This method is invoked for sealed blocks during CPS
            /// construction or during the sealing process in the last step.
            /// </summary>
            /// <param name="incompletePhi">An incomplete phi node to complete.</param>
            /// <param name="markerProvider">A provider of new marker values.</param>
            private void SetupPhiArguments(in IncompletePhi incompletePhi, ref MarkerProvider markerProvider)
            {
                foreach (var predecessor in predecessors)
                {
                    // Get the related predecessor value
                    var value = predecessor.GetValue(incompletePhi.VariableRef, ref markerProvider);

                    // Convert the value into the target type
                    if (incompletePhi.PhiType is PrimitiveType primitiveType)
                        value = Builder.CreateConvert(value, primitiveType);

                    // Set argument value
                    predecessor.SetArgument(incompletePhi.PhiIndex, value);
                }
            }

            /// <summary>
            /// Seals this block (called when all predecessors have been seen) and
            /// wires all (previously unwired) phi nodes.
            /// </summary>
            /// <param name="markerProvider">A provider of new marker values.</param>
            public void Seal(ref MarkerProvider markerProvider)
            {
                Debug.Assert(!IsSealed, "Cannot seal a sealed block");
                foreach (var var in incompletePhis.Values)
                    SetupPhiArguments(var, ref markerProvider);
                IsSealed = true;
                incompletePhis.Clear();
            }

            /// <summary>
            /// Marks the given
            /// </summary>
            /// <param name="returnType">The return type.</param>
            /// <returns>True, iff the return type was not void.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MakeCallTarget(TypeNode returnType)
            {
                if (IsSealed)
                    throw new InvalidOperationException($"Block '{Node}' is sealed");
                var isNotVoid = !returnType.IsVoidType;
                if (IsCallTarget)
                {
                    // Verify call arguments
                    if (FunctionBuilder.ReturnParam.Type != returnType)
                        throw new InvalidOperationException($"Invalid return type '{returnType}'");
                }
                else
                {
                    // Append return-value param
                    if (isNotVoid)
                        FunctionBuilder.InsertParameter(returnType);
                    // Append memory param
                    FunctionBuilder.InsertParameter(FunctionBuilder.IRBuilder.MemoryType);
                }
                IsCallTarget = true;
                return isNotVoid;
            }

            /// <summary>
            /// Finishes all parameters of this block.
            /// </summary>
            public void FinishParameters()
            {
                FunctionBuilder.SealParameters();
            }

            /// <summary>
            /// Finishes this block.
            /// </summary>
            /// <returns>The finished function value.</returns>
            public FunctionValue Finish()
            {
                if (!IsSealed)
                    throw new InvalidOperationException($"Block '{Node}' is not sealed");

                Value call = Terminator as FunctionCall;
                if (call == null)
                {
                    // Build arguments
                    var argsBuilder = ImmutableArray.CreateBuilder<ValueReference>(arguments.Length);
                    for (int i = 0, e = arguments.Length; i < e; ++i)
                    {
                        var argument = arguments[i];
                        Debug.Assert(argument != null, "Invalid CPS argument");
                        argsBuilder.Add(argument);
                    }
                    var args = argsBuilder.MoveToImmutable();

                    switch (successorList.Count)
                    {
                        case 0:
                            throw new InvalidOperationException("Cannot return without a terminator");
                        case 1:
                            // Jump to the next block
                            call = Builder.CreateFunctionCall(
                                successorList[0].FunctionValue,
                                args);
                            break;
                        case 2:
                            if (Terminator == null)
                                throw new InvalidOperationException("Cannot jump to different successors without a target condition");
                            // Branch
                            {
                                Debug.Assert(Terminator.BasicValueType == BasicValueType.Int1, "Invalid branch type");

                                var trueValue = successorList[0].FunctionValue;
                                var falseValue = successorList[1].FunctionValue;

                                call = Builder.CreateFunctionCall(
                                    Terminator,
                                    trueValue,
                                    falseValue,
                                    args);
                            }
                            break;
                        default:
                            if (Terminator == null)
                                throw new InvalidOperationException("Cannot jump to different successors without a target condition");
                            // Switch
                            {
                                Debug.Assert(Terminator.BasicValueType.IsInt(), "Invalid select condition");
                                var targetFunctions = ImmutableArray.CreateBuilder<ValueReference>(successorList.Count);
                                foreach (var successor in successorList)
                                    targetFunctions.Add(successor.FunctionValue);
                                call = Builder.CreateFunctionCall(
                                    Terminator,
                                    targetFunctions.MoveToImmutable(),
                                    args);
                            }
                            break;
                    }
                }

                return FunctionBuilder.Seal(call);
            }

            #endregion

            #region Objects

            /// <summary>
            /// Returns the string representation of this block.
            /// </summary>
            /// <returns>The string representation of this block.</returns>
            public override string ToString()
            {
                return FunctionValue.ToString();
            }

            #endregion
        }

        /// <summary>
        /// Represents a function provider that creates a new function builder for
        /// every child node upon traversal.
        /// </summary>
        private readonly struct NewFunctionProviderPerNode : ICPSBuilderFunctionProvider<TNode>
        {
            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.GetFunctionBuilder(IRBuilder, TNode)"/>
            public FunctionBuilder GetFunctionBuilder(IRBuilder builder, TNode childNode) =>
                builder.CloneAndReplaceFunction(childNode.FunctionValue);

            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.ResolveFunctionCallArgument{TCallback}(FunctionValue, FunctionBuilder, FunctionCall, int, ref TCallback)"/>
            public void ResolveFunctionCallArgument<TCallback>(
                FunctionValue attachedFunction,
                FunctionBuilder targetBuilder,
                FunctionCall call,
                int argumentIdx,
                ref TCallback callback)
                where TCallback : ICPSBuilderFunctionCallArgumentCallback
            {
                var arg = call.GetArgument(argumentIdx);
                callback.AddArgument(arg);
            }
        }

        /// <summary>
        /// Represents an internal argument handler that stores arguments in a list.
        /// </summary>
        private readonly struct CallArgumentHandler : ICPSBuilderFunctionCallArgumentCallback
        {
            /// <summary>
            /// Constructs a new argument handler.
            /// </summary>
            /// <param name="values">The target values.</param>
            public CallArgumentHandler(List<Value> values)
            {
                Values = values;
            }

            /// <summary>
            /// The target values.
            /// </summary>
            public List<Value> Values { get; }
            
            /// <summary cref="ICPSBuilderFunctionCallArgumentCallback.AddArgument(Value)"/>
            public void AddArgument(Value value)
            {
                Values.Add(value);
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new CPS builder.
        /// </summary>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="cfgNode">The entry cfg node.</param>
        /// <returns>The created CPS builder.</returns>
        public static CPSBuilder<TNode, TEnumerator, TVariable> Create(
            IRBuilder builder,
            TNode cfgNode) =>
            Create(builder, cfgNode, new NewFunctionProviderPerNode());

        /// <summary>
        /// Creates a new CPS builder.
        /// </summary>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="cfgNode">The entry cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <returns>The created CPS builder.</returns>
        public static CPSBuilder<TNode, TEnumerator, TVariable> Create<TFunctionProvider>(
            IRBuilder builder,
            TNode cfgNode,
            TFunctionProvider functionProvider)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            var result = new CPSBuilder<TNode, TEnumerator, TVariable>(builder, cfgNode);
            result.Setup(functionProvider);
            return result;
        }

        /// <summary>
        /// Creates a new CPS builder (async)
        /// </summary>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="cfgNode">The entry cfg node.</param>
        /// <returns>The created CPS builder.</returns>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures",
            Justification = "Requires for async processing")]
        public static Task<CPSBuilder<TNode, TEnumerator, TVariable>> CreateAsync(
            IRBuilder builder,
            TNode cfgNode) =>
            CreateAsync(builder, cfgNode, new NewFunctionProviderPerNode());

        /// <summary>
        /// Creates a new CPS builder (async)
        /// </summary>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="cfgNode">The entry cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <returns>The created CPS builder.</returns>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures",
            Justification = "Requires for async processing")]
        public static Task<CPSBuilder<TNode, TEnumerator, TVariable>> CreateAsync<TFunctionProvider>(
            IRBuilder builder,
            TNode cfgNode,
            TFunctionProvider functionProvider)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            return Task.Run(() => Create(builder, cfgNode, functionProvider));
        }

        #endregion

        #region Instance

        private int markerValue = 0;
        private readonly Dictionary<TNode, BasicBlock> cfgMapping =
            new Dictionary<TNode, BasicBlock>();

        /// <summary>
        /// Constructs a new CPS builder.
        /// </summary>
        /// <param name="builder">The associated IR builder.</param>
        /// <param name="cfgNode">The entry cfg node.</param>
        private CPSBuilder(IRBuilder builder, TNode cfgNode)
        {
            Builder = builder ?? throw new ArgumentNullException(nameof(builder));
            EntryNode = cfgNode ?? throw new ArgumentNullException(nameof(cfgNode));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the entry node.
        /// </summary>
        public TNode EntryNode { get; }

        /// <summary>
        /// Returns the associated IR builder.
        /// </summary>
        public IRBuilder Builder { get; }

        /// <summary>
        /// Returns the function builder that is associated with the given node.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <returns>The resolved function builder.</returns>
        public FunctionBuilder this[TNode node] => GetBasicBlock(node).FunctionBuilder;

        /// <summary>
        /// Returns the number of register nodes.
        /// </summary>
        public int Count => cfgMapping.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Dumps all CFG nodes with their according successors.
        /// </summary>
        public void DumpSuccessorsToConsole()
        {
            DumpSuccessors(Console.Out);
        }

        /// <summary>
        /// Dumps all CFG nodes with their according successors.
        /// </summary>
        /// <param name="target">The target writer.</param>
        public void DumpSuccessors(TextWriter target)
        {
            foreach (var block in cfgMapping.Values)
            {
                target.Write(block.Node.NodeIndex);
                target.Write(": ");
                using (var successors = block.Successors)
                {
                    int counter = 0;
                    while (successors.MoveNext())
                    {
                        if (counter++ > 0)
                            target.Write(", ");
                        target.Write(successors.Current.NodeIndex);
                    }
                }
                target.WriteLine();

                Debug.Assert(block.Node == EntryNode || block.NumPredecessors > 0, "Invalid predecessors");
            }
        }

        /// <summary>
        /// Setups the whole builder.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="functionProvider">The function provider.</param>
        private void Setup<TFunctionProvider>(in TFunctionProvider functionProvider)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            // Traverse CFG and setup internal representation
            TraverseIterative(EntryNode, functionProvider, false);

            // Seal the entry block
            Seal(EntryNode);
        }

        /// <summary>
        /// Traverses the given cfg node iteratively while building internal basic blocks.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="cfgNode">The current cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <param name="isProcessed">True, iff the blocks have been processed.</param>
        /// <returns>The created basic block.</returns>
        private void TraverseIterative<TFunctionProvider>(
            TNode cfgNode,
            in TFunctionProvider functionProvider,
            bool isProcessed)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");

            var toProcess = new Queue<(BasicBlock, TNode)>();
            toProcess.Enqueue((null, cfgNode));

            while (toProcess.Count > 0)
            {
                var (parentBlock, currentNode) = toProcess.Dequeue();

                if (TryCreateBasicBlock(currentNode, functionProvider, isProcessed, out BasicBlock basicBlock))
                {
                    using (var successors = currentNode.GetSuccessorEnumerator())
                    {
                        while (successors.MoveNext())
                            toProcess.Enqueue((basicBlock, successors.Current));
                    }
                }

                parentBlock?.AddSuccessor(basicBlock);
            }
        }

        /// <summary>
        /// Creates a basic block from a given cfg node.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="cfgNode">The current cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <param name="isProcessed">True, if the blocks have been processed.</param>
        /// <param name="basicBlock">The created basic block.</param>
        /// <returns>True, if the block was created.</returns>
        private bool TryCreateBasicBlock<TFunctionProvider>(
            TNode cfgNode,
            in TFunctionProvider functionProvider,
            bool isProcessed,
            out BasicBlock basicBlock)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");

            if (cfgMapping.TryGetValue(cfgNode, out basicBlock))
                return false;

            var functionBuilder = functionProvider.GetFunctionBuilder(Builder, cfgNode);
            basicBlock = new BasicBlock(cfgNode, Builder, functionBuilder)
            {
                IsProcessed = isProcessed
            };
            cfgMapping.Add(cfgNode, basicBlock);

            // Setup the corresponding branch condition
            var attachedFunction = cfgNode.FunctionValue;
            if (attachedFunction != null && attachedFunction.HasTarget)
            {
                var call = attachedFunction.Target.ResolveAs<FunctionCall>();
                switch (call.Target.Resolve())
                {
                    case Conditional conditional:
                        basicBlock.Terminator = conditional.Condition;
                        break;
                    case Parameter _:
                    case TopLevelFunction _:
                        basicBlock.Terminator = call;
                        break;
                }

                // Setup arguments
                var args = new List<Value>(call.NumArguments);
                var argumentHandler = new CallArgumentHandler(args);
                for (int i = 0, e = call.NumArguments; i < e; ++i)
                {
                    functionProvider.ResolveFunctionCallArgument(
                        attachedFunction,
                        functionBuilder,
                        call,
                        i,
                        ref argumentHandler);
                }

                for (int i = args.Count - 1; i >= 0; i--)
                    basicBlock.SetArgument(i, args[i]);
            }
            return true;
        }

        /// <summary>
        /// Traverses the given cfg node recursively while building internal basic blocks.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="cfgNode">The current cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <param name="isProcessed">True, iff the blocks have been processed.</param>
        /// <returns>The created basic block.</returns>
        private BasicBlock TraverseRecursive<TFunctionProvider>(
            TNode cfgNode,
            in TFunctionProvider functionProvider,
            bool isProcessed)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");

            if (!TryCreateBasicBlock(cfgNode, functionProvider, isProcessed, out BasicBlock basicBlock))
                return basicBlock;

            using (var successors = cfgNode.GetSuccessorEnumerator())
            {
                while (successors.MoveNext())
                {
                    var successor = TraverseRecursive(successors.Current, functionProvider, isProcessed);
                    basicBlock.AddSuccessor(successor);
                }
            }

            return basicBlock;
        }

        /// <summary>
        /// Appends the given block and its successors.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="cfgNode">The current cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        public void AppendBlock<TFunctionProvider>(
            TNode cfgNode,
            in TFunctionProvider functionProvider)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            AppendBlock(cfgNode, functionProvider, false);
        }

        /// <summary>
        /// Appends the given block and its successors.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="cfgNode">The current cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        /// <param name="isProcessed">True, iff the blocks have been processed.</param>
        public void AppendBlock<TFunctionProvider>(
            TNode cfgNode,
            in TFunctionProvider functionProvider,
            bool isProcessed)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            TraverseRecursive(cfgNode, functionProvider, isProcessed);
        }

        /// <summary>
        /// Links all nodes to the given exit block.
        /// </summary>
        /// <typeparam name="TFunctionProvider">The type of the function provider.</typeparam>
        /// <param name="exitNode">The exit cfg node.</param>
        /// <param name="functionProvider">The function provider.</param>
        public void LinkToExitBlock<TFunctionProvider>(
            TNode exitNode,
            in TFunctionProvider functionProvider)
            where TFunctionProvider : ICPSBuilderFunctionProvider<TNode>
        {
            // Gather all nodes that do not have a successor
            var linkToExitNode = new List<BasicBlock>(Count);
            foreach (var node in cfgMapping.Values)
            {
                if (node.NumSuccessors < 1)
                    linkToExitNode.Add(node);
            }

            // Link all other nodes to the exit node
            var exitBlock = TraverseRecursive(exitNode, functionProvider, false);
            foreach (var link in linkToExitNode)
                link.AddSuccessor(exitBlock);
        }

        /// <summary>
        /// Compute the reverse post order of the registered nodes.
        /// </summary>
        public ImmutableArray<TNode> ComputeReversePostOrder()
        {
            var visitedSet = new bool[Count];
            var postOrder = ImmutableArray.CreateBuilder<TNode>(Count << 1);
            DeterminePostOrder(EntryNode, visitedSet, postOrder);
            postOrder.Reverse();
            return postOrder.ToImmutable();
        }

        /// <summary>
        /// Determines the post order of all nodes.
        /// </summary>

        /// <param name="node">The current node.</param>
        /// <param name="visitedSet">The set of visited nodes.</param>
        /// <param name="builder">The output builder.</param>
        private void DeterminePostOrder(
            TNode node,
            bool[] visitedSet,
            ImmutableArray<TNode>.Builder builder)
        {
            if (!visitedSet[node.NodeIndex])
            {
                visitedSet[node.NodeIndex] = true;
                var successors = GetBasicBlock(node).Successors;
                while (successors.MoveNext())
                {
                    DeterminePostOrder(
                        successors.Current,
                        visitedSet,
                        builder);
                }
            }
            builder.Add(node);
        }

        /// <summary>
        /// Returns the basic block that is associated with the given node.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <returns>The resolved basic block.</returns>
        private BasicBlock GetBasicBlock(TNode node)
        {
            if (!cfgMapping.TryGetValue(node, out BasicBlock block))
                throw new InvalidOperationException();
            return block;
        }

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(TNode node, TVariable var, Value value)
        {
            var block = GetBasicBlock(node);
            block.SetValue(var, value);
        }

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        public Value GetValue(TNode node, TVariable var)
        {
            var block = GetBasicBlock(node);
            var markerProvider = new MarkerProvider(markerValue);
            var result = block.GetValue(var, ref markerProvider);
            markerProvider.Apply(ref markerValue);
            return result;
        }

        /// <summary>
        /// Removes the value of the given variable.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="var">The variable reference.</param>
        public void RemoveValue(TNode node, TVariable var)
        {
            var block = GetBasicBlock(node);
            block.RemoveValue(var);
        }

        /// <summary>
        /// Adds a successor to the given node.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <param name="successor">The successor node.</param>
        public void AddSuccessor(TNode node, TNode successor)
        {
            if (successor == null)
                throw new ArgumentNullException(nameof(successor));
            var block = GetBasicBlock(node);
            var successorBlock = GetBasicBlock(successor);
            block.AddSuccessor(successorBlock);
        }

        /// <summary>
        /// Returns a predecessor enumerator.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <returns>A predecessor enumerator.</returns>
        public Enumerator GetPredecessors(TNode node)
        {
            var block = GetBasicBlock(node);
            return block.Predecessors;
        }

        /// <summary>
        /// Returns a successor enumerator.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <returns>A successor enumerator.</returns>
        public Enumerator GetSuccessors(TNode node)
        {
            var block = GetBasicBlock(node);
            return block.Successors;
        }

        /// <summary>
        /// Tries to process the associated block.
        /// </summary>
        /// <param name="node">The target node.</param>
        public bool Process(TNode node)
        {
            var block = GetBasicBlock(node);
            if (block.IsProcessed)
                return false;
            return block.IsProcessed = true;
        }

        /// <summary>
        /// Tries to seals the associated block.
        /// </summary>
        /// <param name="block">The block to seal.</param>
        private void Seal(BasicBlock block)
        {
            Debug.Assert(block.CanSeal, "Invalid sealing operation");
            var markerProvider = new MarkerProvider(markerValue);
            block.Seal(ref markerProvider);
            markerProvider.Apply(ref markerValue);
        }

        /// <summary>
        /// Tries to seals the associated block.
        /// </summary>
        /// <param name="node">The target node.</param>
        public bool Seal(TNode node)
        {
            var block = GetBasicBlock(node);
            if (block.CanSeal)
            {
                Seal(block);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to process the given node while always trying
        /// to seal the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>True, iff the node has not been processed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ProcessAndSeal(TNode node)
        {
            var block = GetBasicBlock(node);
            if (block.CanSeal)
                Seal(block);
            if (block.IsProcessed)
                return false;
            return block.IsProcessed = true;
        }

        /// <summary>
        /// Sets the given terminator.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="terminator">The terminator to setup.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTerminator(TNode node, Value terminator)
        {
            if (terminator == null)
                throw new ArgumentNullException(nameof(terminator));
            if (terminator.BasicValueType != BasicValueType.Int1 &&
                !terminator.BasicValueType.IsInt() &&
                terminator as FunctionCall == null)
                throw new ArgumentOutOfRangeException(nameof(terminator));
            var block = GetBasicBlock(node);
            block.Terminator = terminator;
        }

        /// <summary>
        /// Makes the given node a call target for a top-level function call.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="returnType">The return type.</param>
        /// <returns>True, iff the return type was not void.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MakeCallTarget(TNode node, TypeNode returnType)
        {
            if (returnType == null)
                throw new ArgumentNullException(nameof(returnType));
            var block = GetBasicBlock(node);
            return block.MakeCallTarget(returnType);
        }

        /// <summary>
        /// Finishes the whole builder.
        /// </summary>
        /// <returns>The finished function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FunctionValue Finish()
        {
            foreach (var block in cfgMapping.Values)
                block.FinishParameters();
            foreach (var block in cfgMapping.Values)
                block.Finish();
            return EntryNode.FunctionValue;
        }

        #endregion
    }
}
