// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CFG.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an abstract interface for all nodes.
    /// </summary>
    public interface ICFGNode
    {
        /// <summary>
        /// Returns the associated function value.
        /// </summary>
        FunctionValue FunctionValue { get; }

        /// <summary>
        /// Returns the zero-based node index that can be used
        /// for fast lookups using arrays.
        /// </summary>
        int NodeIndex { get; }
    }

    /// <summary>
    /// Represents a single node in the scope of a control flow graph.
    /// </summary>
    public sealed class CFGNode : ICPSBuilderNode<CFGNode, CFGNode.Enumerator>
    {
        #region Nested Types

        /// <summary>
        /// Represents a node collection of attached nodes.
        /// </summary>
        public readonly struct NodeCollection : IReadOnlyCollection<CFGNode>
        {
            #region Instance

            internal NodeCollection(List<CFGNode> nodes)
            {
                Nodes = nodes;
            }


            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated node set.
            /// </summary>
            private List<CFGNode> Nodes { get; }

            /// <summary cref="IReadOnlyCollection{T}.Count"/>
            public int Count => Nodes.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Returns a node enumerator to iterate over all attached nodes.
            /// </summary>
            /// <returns>The resulting node enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(Nodes);

            #endregion

            #region IEnumerable

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<CFGNode> IEnumerable<CFGNode>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Represents a child enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<CFGNode>
        {
            #region Instance

            private readonly List<CFGNode> values;
            private List<CFGNode>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new child enumerator.
            /// </summary>
            /// <param name="valueSet">The nodes to iterate over.</param>
            internal Enumerator(List<CFGNode> valueSet)
            {
                values = valueSet;
                enumerator = valueSet.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public CFGNode Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

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
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion

        #region Instance

        private readonly HashSet<CFGNode> successorSet = new HashSet<CFGNode>();
        private readonly List<CFGNode> successors = new List<CFGNode>();
        private readonly List<CFGNode> predecessors = new List<CFGNode>();

        /// <summary>
        /// Constructs a new node.
        /// </summary>
        /// <param name="parent">The parent graph.</param>
        /// <param name="functionValue">The associated function value.</param>
        /// <param name="nodeIndex">The unique node index.</param>
        internal CFGNode(CFG parent, FunctionValue functionValue, int nodeIndex)
        {
            Parent = parent;
            FunctionValue = functionValue;
            NodeIndex = nodeIndex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent cfg.
        /// </summary>
        public CFG Parent { get; }

        /// <summary>
        /// Returns the associated function value.
        /// </summary>
        public FunctionValue FunctionValue { get; }

        /// <summary>
        /// Returns the zero-based node index that can be used
        /// for fast lookups using arrays.
        /// </summary>
        public int NodeIndex { get; }

        /// <summary>
        /// Returns the associated RPO number within the parent CFG.
        /// </summary>
        public int RPONumber { get; internal set; }

        /// <summary>
        /// Returns the predecessors of this node.
        /// </summary>
        public NodeCollection Predecessors => new NodeCollection(predecessors);

        /// <summary>
        /// Returns the successors of this node.
        /// </summary>
        public NodeCollection Successors => new NodeCollection(successors);

        /// <summary>
        /// Returns the number of predecessors.
        /// </summary>
        public int NumPredecessors => predecessors.Count;

        /// <summary>
        /// Returns the number of successors.
        /// </summary>
        public int NumSuccessors => successorSet.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the given node is a registered successor.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>True, if the given node is a registered successor.</returns>
        public bool HasSuccessor(CFGNode node) =>
            successorSet.Contains(node);

        /// <summary>
        /// Adds the given block as successor.
        /// </summary>
        /// <param name="successor">The successor to add.</param>
        internal void AddSuccessor(CFGNode successor)
        {
            Debug.Assert(successor != null, "Invalid successor");
            if (successorSet.Add(successor))
            {
                successors.Add(successor);
                successor.predecessors.Add(this);
            }
        }

        /// <summary>
        /// Returns the successors of this node.
        /// </summary>
        public Enumerator GetSuccessorEnumerator() => new Enumerator(successors);

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this CFG node.
        /// </summary>
        /// <returns>The string representation of this CFG node.</returns>
        public override string ToString()
        {
            return FunctionValue.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Represents a control flow graph.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix",
        Justification = "CFG stands for Control Flow Graph and not CFGCollection or something else")]
    public sealed class CFG : IReadOnlyCollection<CFGNode>
    {
        #region Nested Types

        /// <summary>
        /// Represents a node enumerator to iterate over all nodes
        /// in a control flow graph.
        /// </summary>
        public struct Enumerator : IEnumerator<CFGNode>
        {
            #region Instance

            private Scope.FunctionEnumerator functionEnumerator;

            internal Enumerator(CFG parent)
            {
                Parent = parent;
                functionEnumerator = parent.Scope.Functions.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent graph.
            /// </summary>
            public CFG Parent { get; }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public CFGNode Current => Parent.functionMapping[functionEnumerator.Current];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose()
            {
                functionEnumerator.Dispose();
            }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                return functionEnumerator.MoveNext();
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        /// <summary>
        /// Visits all call targets of a <see cref="FunctionCall"/> and registers
        /// all CFG node successors.
        /// </summary>
        private struct CallTargetVisitor : FunctionCall.ITargetVisitor
        {
            /// <summary>
            /// Constructs a new call target visitor.
            /// </summary>
            /// <param name="cfg">The parent CFG.</param>
            /// <param name="parent">The parent CFG node.</param>
            public CallTargetVisitor(CFG cfg, CFGNode parent)
            {
                CFG = cfg;
                Parent = parent;
            }

            /// <summary>
            /// Returns the associated CFG.
            /// </summary>
            public CFG CFG { get; }

            /// <summary>
            /// Returns the parent CFg node.
            /// </summary>
            public CFGNode Parent { get; }

            /// <summary cref="FunctionCall.ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value callTarget)
            {
                if (callTarget is FunctionValue functionValue &&
                    !functionValue.IsTopLevel)
                {
                    var cfgNode = CFG[functionValue];
                    Parent.AddSuccessor(cfgNode);
                }
                return true;
            }
        }

        /// <summary>
        /// Visits all local function arguments a <see cref="FunctionCall"/> and registers
        /// all CFG node successors.
        /// </summary>
        private struct FunctionArgumentVisitor : FunctionCall.IFunctionArgumentVisitor
        {
            /// <summary>
            /// Constructs a new function argument visitor.
            /// </summary>
            /// <param name="cfg">The parent CFG.</param>
            /// <param name="parent">The parent CFG node.</param>
            public FunctionArgumentVisitor(CFG cfg, CFGNode parent)
            {
                CFG = cfg;
                Parent = parent;
            }

            /// <summary>
            /// Returns the associated CFG.
            /// </summary>
            public CFG CFG { get; }

            /// <summary>
            /// Returns the parent CFg node.
            /// </summary>
            public CFGNode Parent { get; }

            /// <summary cref="FunctionCall.IFunctionArgumentVisitor.VisitFunctionArgument(FunctionValue)"/>
            public void VisitFunctionArgument(FunctionValue functionValue)
            {
                if (functionValue.IsTopLevel)
                    return;

                var cfgNode = CFG[functionValue];
                Parent.AddSuccessor(cfgNode);
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new cfg.
        /// </summary>
        /// <param name="scope">The scope.</param>
        public static CFG Create(Scope scope)
        {
            return new CFG(scope);
        }

        /// <summary>
        /// Creates a new cfg in a separate task.
        /// </summary>
        /// <param name="scope">The scope.</param>
        public static Task<CFG> CreateAsync(Scope scope)
        {
            return Task.Run(() => Create(scope));
        }

        #endregion

        #region Instance

        private readonly Dictionary<FunctionValue, CFGNode> functionMapping =
            new Dictionary<FunctionValue, CFGNode>();

        /// <summary>
        /// Constructs a new CFG.
        /// </summary>
        /// <param name="scope">The source scope.</param>
        private CFG(Scope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            if (scope.Entry as FunctionValue == null)
                throw new NotSupportedException("Invalid scope");

            // Build all nodes
            foreach (var function in Scope.Functions)
            {
                var cfgNode = new CFGNode(this, function, functionMapping.Count);
                functionMapping.Add(function, cfgNode);
            }
            EntryNode = functionMapping[Scope.Entry];

            // Register successors and predecessors
            var rpoNumber = 0;
            foreach (var function in Scope.Functions)
            {
                var node = this[function];
                var targetVisitor = new CallTargetVisitor(
                    this,
                    node);

                var call = function.Target.ResolveAs<FunctionCall>();
                Debug.Assert(call != null, "Invalid call target");

                call.VisitCallTargets(ref targetVisitor);

                // Visit higher order arguments
                var callType = call.Target.Type as FunctionType;
                if (callType.IsHigherOrder)
                {
                    var argumentVisitor = new FunctionArgumentVisitor(
                        this,
                        node);
                    call.VisitFunctionArguments(ref argumentVisitor);
                }

                node.RPONumber = rpoNumber++;
            }

#if VERIFICATION
            // Verify
            foreach (var node in this)
            {
                Debug.Assert(
                    node.NumPredecessors == 0 && node == EntryNode ||
                    node.NumSuccessors == 0 && node.NumPredecessors > 0 ||
                    node.NumPredecessors != 0 && node.NumSuccessors != 0, "Invalid start or exit block");

                // Verify predecessors
                foreach (var predecessor in node.Predecessors)
                    Debug.Assert(predecessor.HasSuccessor(node), "Invalid predecessor");
            }
#endif
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context.
        /// </summary>
        public IRContext Context => Scope.Context;

        /// <summary>
        /// Returns the parent scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the entry point.
        /// </summary>
        public CFGNode EntryNode { get; }

        /// <summary>
        /// Returns the number of nodes in the graph.
        /// </summary>
        public int Count => functionMapping.Count;

        /// <summary>
        /// Resolves the cfg node for the given function value.
        /// </summary>
        /// <param name="functionValue">The function value to resolve.</param>
        /// <returns>The resolved function value.</returns>
        public CFGNode this[FunctionValue functionValue]
        {
            get
            {
                if (functionValue == null)
                    throw new ArgumentNullException(nameof(functionValue));
                if (functionMapping.TryGetValue(functionValue, out CFGNode cfgNode))
                    return cfgNode;
                throw new InvalidOperationException();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a node enumerator to iterate over all nodes
        /// stored in this graph in reverse post order.
        /// </summary>
        /// <returns>The resulting node enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        #endregion

        #region IEnumerable

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<CFGNode> IEnumerable<CFGNode>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
