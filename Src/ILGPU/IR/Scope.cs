// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Scope.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a scope (liveness) analysis.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix",
        Justification = "The name of the analysis is Scope and not ScopeCollection or something else")]
    public sealed class Scope : IReadOnlyCollection<Value>
    {
        #region Nested Types

        /// <summary>
        /// A function enumerator.
        /// </summary>
        public struct FunctionEnumerator : IEnumerator<FunctionValue>
        {
            private int index;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="scope">The parent scope.</param>
            internal FunctionEnumerator(Scope scope)
            {
                Scope = scope;
                index = Scope.functionsInPostOrder.Count;
            }

            /// <summary>
            /// Returns the parent scope;
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the current function.
            /// </summary>
            public FunctionValue Current => Scope.functionsInPostOrder[index];

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext()
            {
                return --index >= 0;
            }

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose() { }
        }

        /// <summary>
        /// Represents a collection of functions within a scope.
        /// </summary>
        public readonly struct FunctionCollection : IReadOnlyCollection<FunctionValue>
        {
            /// <summary>
            /// Constructs a new function collection.
            /// </summary>
            /// <param name="scope">The parent scope.</param>
            internal FunctionCollection(Scope scope)
            {
                Scope = scope;
            }

            /// <summary>
            /// Returns the parent scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the n
            /// </summary>
            public int Count => Scope.NumFunctions;

            int IReadOnlyCollection<FunctionValue>.Count => throw new NotSupportedException();

            /// <summary>
            /// Returns a function enumerator.
            /// </summary>
            /// <returns>The function enumerator.</returns>
            public FunctionEnumerator GetEnumerator() => new FunctionEnumerator(Scope);

            IEnumerator<FunctionValue> IEnumerable<FunctionValue>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Enumerates all nodes in a scope in post order.
        /// </summary>
        public struct PostOrderEnumerator : IEnumerator<Value>
        {
            #region Instance

            private int index;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="scope">The parent scope.</param>
            internal PostOrderEnumerator(Scope scope)
            {
                Debug.Assert(scope != null, "Invalid scope");
                Scope = scope;
                index = -1;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent scope;
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public Value Current => Scope.postOrder[index];

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext()
            {
                return ++index < Scope.Count;
            }

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose() { }

            #endregion
        }

        /// <summary>
        /// Enumerates all nodes in a scope in reverse post order.
        /// </summary>
        public struct Enumerator : IEnumerator<Value>
        {
            #region Instance

            private int index;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="scope">The parent scope.</param>
            internal Enumerator(Scope scope)
            {
                Debug.Assert(scope != null, "Invalid scope");
                Scope = scope;
                index = scope.Count;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent scope;
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public Value Current => Scope.postOrder[index];

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext()
            {
                return --index >= 0;
            }

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose() { }

            #endregion
        }

        /// <summary>
        /// Represents a visitor for function calls.
        /// </summary>
        public interface IFunctionCallVisitor
        {
            /// <summary>
            /// Visits the given function call.
            /// </summary>
            /// <param name="functionCall">The function call.</param>
            /// <returns>True, iff the process should be continued.</returns>
            bool Visit(FunctionCall functionCall);
        }

        /// <summary>
        /// Represents a visitor for memory chains.
        /// </summary>
        public interface IMemoryChainVisitor
        {
            /// <summary>
            /// Visits the given memory reference.
            /// </summary>
            /// <param name="memoryRef">The memory reference to visit.</param>
            /// <returns>True, iff the process should be continued.</returns>
            bool Visit(MemoryRef memoryRef);

            /// <summary>
            /// Visits the given memory value.
            /// </summary>
            /// <param name="memoryValue">The memory value to visit.</param>
            /// <returns>True, iff the process should be continued.</returns>
            bool Visit(MemoryValue memoryValue);
        }

        /// <summary>
        /// Represents a visitor that can visit different memory chains.
        /// </summary>
        /// <typeparam name="TVisitor">The nested visitor type.</typeparam>
        public interface IMultiMemoryChainVisitor<TVisitor>
            where TVisitor : IMemoryChainVisitor
        {
            /// <summary>
            /// Visits a new memory chain.
            /// </summary>
            /// <param name="functionValue">The entry point.</param>
            /// <param name="memoryParameter">The chain start.</param>
            /// <returns>A visitor that should be used for this chain.</returns>
            TVisitor VisitMemoryChain(FunctionValue functionValue, Parameter memoryParameter);
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new scope.
        /// </summary>
        /// <param name="builder">The IR builder.</param>
        /// <param name="entry">The entry point.</param>
        public static Scope Create(IRBuilder builder, FunctionValue entry)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            return Create(builder.Context, entry);
        }

        /// <summary>
        /// Creates a new scope.
        /// </summary>
        /// <param name="context">The IR context.</param>
        /// <param name="entry">The entry point.</param>
        public static Scope Create(IRContext context, FunctionValue entry)
        {
            return new Scope(context, entry);
        }

        /// <summary>
        /// Creates a new scope in a separate task.
        /// </summary>
        /// <param name="context">The IR context.</param>
        /// <param name="entry">The entry point.</param>
        public static Task<Scope> CreateAsync(IRContext context, FunctionValue entry)
        {
            return Task.Run(() => Create(context, entry));
        }

        #endregion

        #region Instance

        private readonly HashSet<Value> nodes = new HashSet<Value>();
        private readonly List<FunctionValue> functionsInPostOrder = new List<FunctionValue>(10);
        private readonly List<Value> postOrder;

        /// <summary>
        /// Constructs a new scope.
        /// </summary>
        /// <param name="context">The IR context.</param>
        /// <param name="entry">The entry point.</param>
        private Scope(IRContext context, FunctionValue entry)
        {
            Context = context ??  throw new ArgumentNullException(nameof(context));
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));

            // Note that we cannot use a node marker in this case since
            // multiple analyses may run on nested nodes at the same time.
            var nodeSet = new HashSet<Value>();
            var nodeCount = GatherNodes(nodeSet);
            postOrder = new List<Value>(nodeCount);
            ProcessReachable(entry, nodeSet);

#if DEBUG
            foreach (var function in Functions)
                Debug.Assert(!function.IsTopLevel || function == Entry, "Invalid top level function in scope");
#endif
        }

        /// <summary>
        /// Performs the scope analysis on the entry point.
        /// </summary>
        /// <param name="visitedNodes">The marker for visited nodes.</param>
        /// <returns>The number of found nodes.</returns>
        private int GatherNodes(HashSet<Value> visitedNodes)
        {
            visitedNodes.Add(Entry);

            var processed = new Queue<Value>();
            foreach (var param in Entry.AttachedParameters)
                processed.Enqueue(param.Resolve());

            int result = 0;
            while (processed.Count > 0)
            {
                var current = processed.Dequeue();

                var function = current as FunctionValue;
                if (function != null &&
                    function.IsTopLevel &&
                    function != Entry)
                    continue;

                if (!visitedNodes.Add(current))
                    continue;

                if (function != null)
                {
                    foreach (var param in function.AttachedParameters)
                        processed.Enqueue(param.Resolve());
                }
                foreach (var use in current.Uses)
                    processed.Enqueue(use.Resolve());

                ++result;
            }
            return result;
        }

        /// <summary>
        /// Performs a reachability analysis.
        /// </summary>
        /// <param name="rootNode">The node to process.</param>
        /// <param name="foundNodes">The set of found nodes.</param>
        private void ProcessReachable(Value rootNode, HashSet<Value> foundNodes)
        {
            var processed = new Stack<(Value, int)>(foundNodes.Count << 2);
            var current = (Node: rootNode, Child: 0);

            while (true)
            {
                var currentNode = current.Node;
                if (!foundNodes.Contains(currentNode) && !currentNode.IsConstant() ||
                    current.Child == 0 && !nodes.Add(currentNode))
                    goto next;

                if (current.Child >= currentNode.Nodes.Length)
                {
                    if (currentNode is FunctionValue function)
                    {
                        Debug.Assert(!function.IsTopLevel || function == Entry);
                        functionsInPostOrder.Add(function);
                    }
                    postOrder.Add(currentNode);
                    goto next;
                }
                else
                {
                    processed.Push((current.Node, current.Child + 1));
                    current = (current.Node[current.Child], 0);
                }

                continue;
                next:
                if (processed.Count < 1)
                    break;
                current = processed.Pop();
            }

            Debug.Assert(nodes.Contains(Entry));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the entry point.
        /// </summary>
        public FunctionValue Entry { get; }

        /// <summary>
        /// Returns the number of nodes in this scope.
        /// </summary>
        public int Count => nodes.Count;

        /// <summary>
        /// Accesses all functions in this scope in reverse post order.
        /// </summary>
        public FunctionCollection Functions => new FunctionCollection(this);

        /// <summary>
        /// Returns the number of functions in this scope.
        /// </summary>
        public int NumFunctions => functionsInPostOrder.Count;

        /// <summary>
        /// Accesses all nodes in post order.
        /// </summary>
        public PostOrderEnumerator PostOrder => new PostOrderEnumerator(this);

        #endregion

        #region Methods

        /// <summary>
        /// Computes all function references of the entry node.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>The computed function references.</returns>
        public FunctionReferences ComputeFunctionReferences<TPredicate>(TPredicate predicate)
            where TPredicate : IFunctionCollectionPredicate =>
            FunctionReferences.Create(this, predicate);

        /// <summary>
        /// Returns true iff the given node is contained in this scope.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>True, iff the given node is contained in this scope.</returns>
        public bool Contains(Value node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            return nodes.Contains(node);
        }

        /// <summary>
        /// Returns a use collection fir the given node in the context
        /// of this scope.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>A use collection of the given node in the context of this scope.</returns>
        public ScopeUseCollection GetUses(Value node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            return new ScopeUseCollection(this, node);
        }

        /// <summary>
        /// Returns a use enumerator for the given node in the context
        /// of this scope.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>A use enumerator of the given node in the context of this scope.</returns>
        public ScopeUseCollection.Enumerator GetUsesEnumerator(Value node)
        {
            return GetUses(node).GetEnumerator();
        }

        /// <summary>
        /// Visits all nodes in this scope in reverse post order.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="visitor">The visitor instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitNodes<T>(T visitor)
            where T : IValueVisitor
        {
            foreach (var node in this)
                node.Accept(visitor);
        }

        /// <summary>
        /// Visits all function calls in this scope.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitFunctionCalls<TVisitor>(ref TVisitor visitor)
            where TVisitor : IFunctionCallVisitor
        {
            foreach (var node in this)
            {
                if (node is FunctionCall call)
                    visitor.Visit(call);
            }
        }

        /// <summary>
        /// Visits all function call targets in this scope.
        /// </summary>
        /// <typeparam name="TVisitor">The visitor type.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitCallTargets<TVisitor>(ref TVisitor visitor)
            where TVisitor : FunctionCall.ITargetVisitor
        {
            foreach (var node in this)
            {
                if (node is FunctionCall call)
                    call.VisitCallTargets(ref visitor);
            }
        }

        /// <summary>
        /// Visits all function call targets and arguments in this scope.
        /// </summary>
        /// <typeparam name="TTargetVisitor">The target visitor type.</typeparam>
        /// <typeparam name="TFunctionArgumentVisitor">The function argument visitor type.</typeparam>
        /// <param name="targetVisitor">The target visitor.</param>
        /// <param name="functionArgumentVisitor">The argument visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitCallTargetsAndFunctionArguments<TTargetVisitor, TFunctionArgumentVisitor>(
            ref TTargetVisitor targetVisitor,
            ref TFunctionArgumentVisitor functionArgumentVisitor)
            where TTargetVisitor : FunctionCall.ITargetVisitor
            where TFunctionArgumentVisitor : FunctionCall.IFunctionArgumentVisitor
        {
            foreach (var node in this)
            {
                if (node is FunctionCall call)
                {
                    call.VisitCallTargets(ref targetVisitor);
                    call.VisitFunctionArguments(ref functionArgumentVisitor);
                }
            }
        }

        /// <summary>
        /// Visits all function call arguments in this scope.
        /// </summary>
        /// <typeparam name="TFunctionArgumentVisitor">The function argument visitor type.</typeparam>
        /// <param name="functionArgumentVisitor">The argument visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitFunctionArguments<TFunctionArgumentVisitor>(
            ref TFunctionArgumentVisitor functionArgumentVisitor)
            where TFunctionArgumentVisitor : FunctionCall.IFunctionArgumentVisitor
        {
            foreach (var node in this)
            {
                if (node is FunctionCall call)
                    call.VisitFunctionArguments(ref functionArgumentVisitor);
            }
        }

        /// <summary>
        /// Visits all function calls that are used by the given node.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="node">The current node.</param>
        /// <param name="visitor">The visitor.</param>
        public void VisitUsedFunctionCalls<T>(Value node, ref T visitor)
            where T : struct, IFunctionCallVisitor
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            VisitFunctionCallsInternal(node, ref visitor);
        }

        /// <summary>
        /// Visits all function calls that are used by the given node.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="node">The current node.</param>
        /// <param name="visitor">The visitor.</param>
        private bool VisitFunctionCallsInternal<T>(Value node, ref T visitor)
            where T : struct, IFunctionCallVisitor
        {
            using (var usesEnumerator = GetUsesEnumerator(node))
            {
                while (usesEnumerator.MoveNext())
                {
                    var current = usesEnumerator.Current.Resolve();
                    if (current is FunctionCall call)
                    {
                        if (!visitor.Visit(call))
                            return false;
                    }
                    else if (!VisitFunctionCallsInternal(current, ref visitor))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Visits all memory chains in the current scope.
        /// </summary>
        /// <typeparam name="T">The visitor type for all chains.</typeparam>
        /// <typeparam name="TVisitor">The visitor type for a single chain.</typeparam>
        /// <param name="chainVisitor">The chain visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitMemoryChains<T, TVisitor>(ref T chainVisitor)
            where T : IMultiMemoryChainVisitor<TVisitor>
            where TVisitor : IMemoryChainVisitor
        {
            var processingStack = new Stack<Value>();
            foreach (var functionValue in Functions)
            {
                if (functionValue.Parameters.TryResolveMemoryParameter(out Parameter memoryParameter))
                {
                    var visitor = chainVisitor.VisitMemoryChain(functionValue, memoryParameter);
                    VisitMemoryChainInternal(processingStack, memoryParameter, ref visitor);
                    processingStack.Clear();
                }
            }
        }

        /// <summary>
        /// Visits all nodes in the induced memory chain.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="node">The current node.</param>
        /// <param name="visitor">The visitor.</param>
        public void VisitMemoryChain<T>(Value node, ref T visitor)
            where T : IMemoryChainVisitor
        {
            if (!MemoryRef.IsMemoryChainMember(node))
                throw new ArgumentOutOfRangeException(nameof(node));
            var processingStack = new Stack<Value>();
            VisitMemoryChainInternal(processingStack, node, ref visitor);
        }

        /// <summary>
        /// Visits all nodes in the induced memory chain.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="processingStack">The current processing stack.</param>
        /// <param name="node">The current node.</param>
        /// <param name="visitor">The visitor.</param>
        private void VisitMemoryChainInternal<T>(Stack<Value> processingStack, Value node, ref T visitor)
            where T : IMemoryChainVisitor
        {
            for (; ; )
            {
                if (node is MemoryRef memoryRef)
                {
                    if (!visitor.Visit(memoryRef))
                        break;

                    // The children are memory nodes
                    foreach (var use in GetUses(node))
                    {
                        var otherNode = use.Resolve();
                        if (MemoryRef.IsMemoryChainMember(otherNode))
                            processingStack.Push(otherNode);
                    }
                }
                else
                {
                    Debug.Assert(MemoryRef.IsMemoryChainMember(node), "Invalid memory chain");
                    if (node is MemoryValue memoryValue && !visitor.Visit(memoryValue))
                        break;

                    // We are looking for a memory reference child
                    foreach (var use in GetUses(node))
                    {
                        var otherNode = use.Resolve();
                        if (otherNode is MemoryRef)
                            processingStack.Push(otherNode);
                    }
                }

                if (processingStack.Count < 1)
                    break;
                node = processingStack.Pop();
            }
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all nodes in this scope
        /// in reverse post order.
        /// </summary>
        /// <returns>An enumerator that enumerates all nodes in this scope.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Value> IEnumerable<Value>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this scope.
        /// </summary>
        /// <returns>The string representation of this scope.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append("Entry: ");
            builder.AppendLine(Entry.ToString());

            foreach (var node in postOrder)
            {
                if (node == Entry || node is Parameter || node is FunctionCall)
                    continue;
                if (node as FunctionValue == null)
                    builder.Append('\t');
                builder.AppendLine(node.ToString());
            }
            return builder.ToString();
        }

        #endregion
    }
}
