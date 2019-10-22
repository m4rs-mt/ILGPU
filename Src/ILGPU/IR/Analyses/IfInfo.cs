// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IfInfo.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// A simple if-information object.
    /// </summary>
    public readonly struct IfInfo
    {
        #region Instance

        /// <summary>
        /// Constructs a new if-information object.
        /// </summary>
        /// <param name="condition">The if condition.</param>
        /// <param name="entryBlock">The entry block using the condition.</param>
        /// <param name="ifBlock">The if block.</param>
        /// <param name="elseBlock">The else block (if any).</param>
        /// <param name="exitBlock">The exit block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IfInfo(
            Value condition,
            BasicBlock entryBlock,
            BasicBlock ifBlock,
            BasicBlock elseBlock,
            BasicBlock exitBlock)
        {
            Debug.Assert(condition != null, "Invalid condition");
            Debug.Assert(entryBlock != null, "Invalid entry block");

            Condition = condition;
            EntryBlock = entryBlock;
            IfBlock = ifBlock;
            ElseBlock = elseBlock;
            ExitBlock = exitBlock;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The basic if condition.
        /// </summary>
        public Value Condition { get; }

        /// <summary>
        /// The entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// The if block.
        /// </summary>
        public BasicBlock IfBlock { get; }

        /// <summary>
        /// The final else block.
        /// </summary>
        public BasicBlock ElseBlock { get; }

        /// <summary>
        /// The final exit block (continue target).
        /// </summary>
        public BasicBlock ExitBlock { get; }

        /// <summary>
        /// Returns true if the current if has an else block.
        /// </summary>
        public bool HasElseBlock => ElseBlock != null;

        #endregion

        #region Methods

        /// <summary>
        /// Returns tue if this is a simple if. A simple if is directly
        /// connected to both branch blocks. Furthermore, each branch block
        /// is directly linked to exit block.
        /// </summary>
        /// <returns>True, if this is a simple if.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSimpleIf() =>
            HasElseBlock &&
            // Check direct connection of the entry node
            EntryBlock.Successors[0] == IfBlock &&
            EntryBlock.Successors[1] == ElseBlock &&
            // Check direct connection of the exit node
            IfBlock.Successors.Length == 1 &&
            IfBlock.Successors[0] == ExitBlock &&
            ElseBlock.Successors.Length == 1 &&
            ElseBlock.Successors[0] == ExitBlock;

        /// <summary>
        /// Returns true if this if has side effects.
        /// </summary>
        /// <returns>True, if this if has side effects.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSideEffects() =>
            IfBlock.HasSideEffects() ||
            (HasElseBlock && ElseBlock.HasSideEffects());

        /// <summary>
        /// Resolves detailed variable information.
        /// </summary>
        /// <returns>The resolved variable information.</returns>
        public IfVariableInfo ResolveVariableInfo() =>
            new IfVariableInfo(this);

        #endregion
    }

    /// <summary>
    /// Represents detailed variable information with respect
    /// to an if statement.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is a single variable information object; adding a collection suffix would be misleading")]
    public readonly struct IfVariableInfo : IReadOnlyCollection<KeyValuePair<PhiValue, IfVariableInfo.Variable>>
    {
        #region Nested Types

        /// <summary>
        /// A single if variable.
        /// </summary>
        public readonly struct Variable
        {
            /// <summary>
            /// Constructs a new variable.
            /// </summary>
            /// <param name="trueValue">The true value.</param>
            internal Variable(Value trueValue)
                : this(trueValue, null)
            { }

            /// <summary>
            /// Constructs a new variable.
            /// </summary>
            /// <param name="trueValue">The true value.</param>
            /// <param name="falseValue">The false value.</param>
            private Variable(Value trueValue, Value falseValue)
            {
                TrueValue = trueValue;
                FalseValue = falseValue;
            }

            /// <summary>
            /// The value from the true branch.
            /// </summary>
            public Value TrueValue { get; }

            /// <summary>
            /// The value from the false branch.
            /// </summary>
            public Value FalseValue { get; }

            internal Variable AddFalseValue(Value falseValue)
            {
                Debug.Assert(FalseValue == null, "False value already specified");
                return new Variable(TrueValue, falseValue);
            }
        }

        /// <summary>
        /// An enumerator to iterate over all variables.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<PhiValue, Variable>>
        {
            private readonly Dictionary<PhiValue, Variable> variablesInfo;
            private List<PhiValue>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new variable enumerator.
            /// </summary>
            /// <param name="info">The parent info instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(in IfVariableInfo info)
            {
                variablesInfo = info.variablesInfo;
                enumerator = info.variables.GetEnumerator();
            }

            /// <summary>
            /// Returns the current info.
            /// </summary>
            public KeyValuePair<PhiValue, Variable> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var currentEntry = enumerator.Current;
                    return new KeyValuePair<PhiValue, Variable>(
                        currentEntry,
                        variablesInfo[currentEntry]);
                }
            }

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        #region Instance

        private readonly HashSet<Value> variableValues;
        private readonly Dictionary<PhiValue, Variable> variablesInfo;
        private readonly List<PhiValue> variables;

        /// <summary>
        /// Constructs a new detailed variable information instance.
        /// </summary>
        /// <param name="ifInfo">The variable info.</param>
        internal IfVariableInfo(in IfInfo ifInfo)
        {
            Debug.Assert(ifInfo.HasElseBlock, "Invalid variable information");
            int capacity = IntrinsicMath.Max(
                ifInfo.IfBlock.Count,
                ifInfo.ElseBlock.Count);

            variableValues = new HashSet<Value>();
            variablesInfo = new Dictionary<PhiValue, Variable>(capacity);
            variables = new List<PhiValue>(capacity);

            // Link if block
            foreach (Value value in ifInfo.IfBlock)
            {
                foreach (var use in value.Uses)
                {
                    if (use.Resolve() is PhiValue phiValue &&
                        phiValue.Nodes.Length == 2)
                    {
                        Debug.Assert(
                            !variablesInfo.ContainsKey(phiValue),
                            "Invalid variable linking");

                        variableValues.Add(value);
                        variablesInfo.Add(phiValue, new Variable(value));
                        variables.Add(phiValue);
                    }
                }
            }

            // Link else block
            foreach (Value value in ifInfo.ElseBlock)
            {
                foreach (var use in value.Uses)
                {
                    // Does this phi belong to this if?
                    if (use.Resolve() is PhiValue phiValue &&
                        phiValue.Nodes.Length == 2 &&
                        variablesInfo.TryGetValue(phiValue, out var entry))
                    {
                        variableValues.Add(value);
                        variablesInfo[phiValue] = entry.AddFalseValue(value);
                    }
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of phi values.
        /// </summary>
        public int Count => variablesInfo.Count;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates over all variables.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<KeyValuePair<PhiValue, Variable>> IEnumerable<KeyValuePair<PhiValue, Variable>>.
            GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Inferes high-level control-flow ifs
    /// from unstructred low-level control flow.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of this program analysis")]
    public sealed class IfInfos : IReadOnlyList<IfInfo>
    {
        #region Nested Types

        /// <summary>
        /// An enumerator to iterate over all ifs.
        /// </summary>
        public struct Enumerator : IEnumerator<IfInfo>
        {
            private List<IfInfo>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new info enumerator.
            /// </summary>
            /// <param name="infos">The infos to iterate over.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(List<IfInfo> infos)
            {
                enumerator = infos.GetEnumerator();
            }

            /// <summary>
            /// Returns the current info.
            /// </summary>
            public IfInfo Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new if infos instance.
        /// </summary>
        /// <param name="cfg">The current CFG.</param>
        /// <returns>The created info instance.</returns>
        public static IfInfos Create(CFG cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));
            return Create(Dominators.Create(cfg));
        }

        /// <summary>
        /// Creates a new if infos instance.
        /// </summary>
        /// <param name="dominators">The current dominators.</param>
        /// <returns>The created info instance.</returns>
        public static IfInfos Create(Dominators dominators)
        {
            if (dominators == null)
                throw new ArgumentNullException(nameof(dominators));
            return new IfInfos(dominators);
        }

        /// <summary>
        /// Tries to create a new if-info instance.
        /// </summary>
        /// <param name="dominators">The dominators.</param>
        /// <param name="exitNode">The exit node.</param>
        /// <param name="ifInfo">The resulting if info.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCreate(
            Dominators dominators,
            CFG.Node exitNode,
            out IfInfo ifInfo)
        {
            Debug.Assert(dominators != null, "Invalid dominators");
            Debug.Assert(exitNode != null, "Invalid exit node");

            ifInfo = default;

            // Check whether this node can be the exit node of an if branch
            if (exitNode.NumPredecessors != 2)
                return false;

            // Interpret each predecessor as one branch
            var truePred = exitNode.Predecessors[0];
            var falsePred = exitNode.Predecessors[1];

            // Try to resolve the branch node
            var entryNode = dominators.GetImmediateCommonDominator(truePred, falsePred);
            if (entryNode.NumSuccessors != 2)
                return false;

            var entryBlock = entryNode.Block;
            if (!(entryBlock.Terminator is ConditionalBranch branch))
                return false;

            ifInfo = new IfInfo(
                branch.Condition,
                entryBlock,
                branch.TrueTarget,
                branch.FalseTarget,
                exitNode.Block);

            return true;
        }

        #endregion

        #region Instance

        private readonly Dictionary<CFG.Node, IfInfo> ifs;
        private readonly List<IfInfo> infos;

        /// <summary>
        /// Constructs a new info infos instance.
        /// </summary>
        /// <param name="dominators">The source dominators.</param>
        private IfInfos(Dominators dominators)
        {
            Debug.Assert(dominators != null, "Invalid dominators");
            Dominators = dominators;

            var cfg = dominators.CFG;
            ifs = new Dictionary<CFG.Node, IfInfo>(cfg.Count);
            infos = new List<IfInfo>(cfg.Count);

            foreach (var exitNode in cfg)
            {
                // True to resolve if information for the current node
                if (TryCreate(dominators, exitNode, out var ifInfo))
                {
                    ifs.Add(exitNode, ifInfo);
                    infos.Add(ifInfo);
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated dominators.
        /// </summary>
        public Dominators Dominators { get; }

        /// <summary>
        /// Returns the number of info objects.
        /// </summary>
        public int Count => infos.Count;

        /// <summary>
        /// Lookups the given if-info index.
        /// </summary>
        /// <param name="index">The if-info index.</param>
        /// <returns>The resolved if info.</returns>
        public IfInfo this[int index] => infos[index];

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the given node to an if-info instance.
        /// </summary>
        /// <param name="node">The node to lookup.</param>
        /// <param name="ifInfo">The resolved if info (if any).</param>
        /// <returns>True, if any if info could be resolved.</returns>
        public bool TryGetIfInfo(CFG.Node node, out IfInfo ifInfo) =>
            ifs.TryGetValue(node, out ifInfo);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates over all infos.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(infos);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<IfInfo> IEnumerable<IfInfo>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}