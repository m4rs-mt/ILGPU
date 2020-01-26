// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LoopInfo.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// A simple loop info object.
    /// </summary>
    public readonly struct LoopInfo
    {
        #region Static

        /// <summary>
        /// Creates a new loop info instance from the given
        /// scc while checking for unique entry and exit blocks.
        /// </summary>
        /// <param name="scc">The scc.</param>
        /// <returns>The resolved loop info instance.</returns>
        public static LoopInfo Create(SCCs.SCC scc)
        {
            if (!TryCreate(scc, out var loopInfo))
                throw new InvalidKernelOperationException();
            return loopInfo;
        }

        /// <summary>
        /// Tries to create a new loop info instance from the given
        /// scc while checking for unique entry and exit blocks.
        /// </summary>
        /// <param name="scc">The scc.</param>
        /// <param name="loopInfo">The resolved loop info object (if any).</param>
        /// <returns>True, if the resulting loop info object could be resolved.</returns>
        public static bool TryCreate(SCCs.SCC scc, out LoopInfo loopInfo)
        {
            loopInfo = default;

            if (!TryGetEntryBlock(scc, out var entryBlock) ||
                !TryGetExitBlock(scc, out var exitBlock))
                return false;
            loopInfo = new LoopInfo(scc, entryBlock, exitBlock);
            return true;
        }

        /// <summary>
        /// Checks whether the given node is a loop entry point.
        /// </summary>
        /// <param name="scc">All SCCS.</param>
        /// <param name="entryPoint">The resolved entry point (if any).</param>
        /// <returns>True, if the given node is a loop entry point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetEntryBlock(
            SCCs.SCC scc,
            out BasicBlock entryPoint)
        {
            entryPoint = null;
            foreach (var node in scc)
            {
                foreach (var pred in node.Predecessors)
                {
                    if (scc.Contains(pred))
                        continue;

                    // Multiple loop entry points
                    if (entryPoint != null & entryPoint != pred.Block)
                        return false;
                    entryPoint = pred.Block;
                }
            }
            return true;
        }

        /// <summary>
        /// Tries to resolve a unique exit block.
        /// </summary>
        /// <param name="scc">The current SCC.</param>
        /// <param name="exitBlock">The resolved exit block (if any).</param>
        /// <returns>True, if an unique break node could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetExitBlock(
            SCCs.SCC scc,
            out BasicBlock exitBlock)
        {
            exitBlock = null;
            foreach (var node in scc)
            {
                foreach (var succ in node.Successors)
                {
                    BasicBlock succBlock = succ.Block;
                    if (!scc.Contains(succBlock))
                    {
                        if (exitBlock != null && exitBlock != succBlock)
                            return false;
                        exitBlock = succBlock;
                    }
                }
            }
            return exitBlock != null;
        }

        /// <summary>
        /// Checks whether the given phi value can be resolved
        /// to an induction variable.
        /// </summary>
        /// <param name="scc">The related SCC.</param>
        /// <param name="variableIndex">The variable index.</param>
        /// <param name="visitedNodes">The set of already visited nodes.</param>
        /// <param name="phiValue">The current phi value.</param>
        /// <param name="inductionVariable">The resolved induction variable (if any).</param>
        /// <returns>True, if the given phi node could be resolved to an induction variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveInductionVariable(
            in SCCs.SCC scc,
            int variableIndex,
            HashSet<Node> visitedNodes,
            PhiValue phiValue,
            out InductionVariable inductionVariable)
        {
            // Search for two operands of which one is defined
            // outside the current SCC and one is from the
            // inside of the current SCC
            inductionVariable = default;
            int numOperands = phiValue.Nodes.Length;
            if (numOperands != 2)
                return false;

            Value outsideOperand = null;
            Value insideOperand = null;
            for (int i = 0; i < numOperands; ++i)
            {
                Value operand = phiValue.Nodes[i];
                if (!scc.Contains(operand.BasicBlock))
                    outsideOperand = operand;
                else
                    insideOperand = operand;
            }

            if (insideOperand == null | outsideOperand == null)
                return false;

            // Check the influcence of the inside operand
            // on the overall break behavior
            bool foundBranch = false;
            foreach (var use in phiValue.Uses)
            {
                var node = use.Resolve();
                visitedNodes.Clear();
                if (IsInductionVariableBranch(scc, visitedNodes, node))
                {
                    // Check for a trivial induction branch
                    if (foundBranch)
                        return false;

                    inductionVariable = new InductionVariable(
                        variableIndex,
                        phiValue,
                        outsideOperand,
                        insideOperand,
                        node);
                    foundBranch = true;
                }
            }

            return foundBranch;
        }

        /// <summary>
        /// Tries to trace an induction-variable branch.
        /// </summary>
        /// <param name="scc">The current scc.</param>
        /// <param name="visitedNodes">The set of already visited nodes.</param>
        /// <param name="node">The node to trace.</param>
        /// <returns>True, if the given node is an induction-variable branch.</returns>
        private static bool IsInductionVariableBranch(
            in SCCs.SCC scc,
            HashSet<Node> visitedNodes,
            Value node)
        {
            if (!visitedNodes.Add(node))
                return false;

            // Try to find a conditional branch that leaves the current scc
            if (node is Branch branch && branch.NumTargets > 1)
            {
                foreach (var target in branch.Targets)
                {
                    if (!scc.Contains(target))
                        return true;
                }
            }

            // Iterate over all uses to find a recursive trace
            foreach (var use in node.Uses)
            {
                if (IsInductionVariableBranch(scc, visitedNodes, use.Resolve()))
                    return true;
            }

            return false;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new loop info instance.
        /// </summary>
        /// <param name="scc">The parent scc.</param>
        /// <param name="entryBlock">The unique entry block.</param>
        /// <param name="exitBlock">The unique exit block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LoopInfo(
            SCCs.SCC scc,
            BasicBlock entryBlock,
            BasicBlock exitBlock)
        {
            SCC = scc;
            EntryBlock = entryBlock;
            ExitBlock = exitBlock;

            var phis = scc.ResolvePhis();

            var inductionVariables = ImmutableArray.CreateBuilder<InductionVariable>(phis.Count);
            var visitedNodes = new HashSet<Node>();
            foreach (var phi in phis)
            {
                // Check whether this phi is an induction variable
                if (TryResolveInductionVariable(
                    scc,
                    inductionVariables.Count,
                    visitedNodes,
                    phi,
                    out var variable))
                {
                    inductionVariables.Add(variable);
                }
            }
            InductionVariables = inductionVariables.ToImmutable();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated SCC.
        /// </summary>
        public SCCs.SCC SCC { get; }

        /// <summary>
        /// Returns the entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// Returns the exit block.
        /// </summary>
        public BasicBlock ExitBlock { get; }

        /// <summary>
        /// Returns all underlying induction variables.
        /// </summary>
        public ImmutableArray<InductionVariable> InductionVariables { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the given block belongs to the associated SCC.
        /// </summary>
        /// <param name="block">The block to map to an SCC.</param>
        /// <returns>True, if the node belongs to the associated SCC.</returns>
        public bool Contains(BasicBlock block) => SCC.Contains(block);

        #endregion
    }

    /// <summary>
    /// Inferes high-level control-flow loops
    /// from unstructred low-level control flow.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of this program analysis")]
    public sealed class LoopInfos : IReadOnlyList<LoopInfo>
    {
        #region Nested Types

        /// <summary>
        /// An enumerator to iterate over all SCCs.
        /// </summary>
        public struct Enumerator : IEnumerator<LoopInfo>
        {
            private List<LoopInfo>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new info enumerator.
            /// </summary>
            /// <param name="infos">The infos to iterate over.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(List<LoopInfo> infos)
            {
                enumerator = infos.GetEnumerator();
            }

            /// <summary>
            /// Returns the current info.
            /// </summary>
            public LoopInfo Current => enumerator.Current;

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
        /// Creates a new loop infos instance.
        /// </summary>
        /// <param name="cfg">The current CFG.</param>
        /// <returns>The created info instance.</returns>
        public static LoopInfos Create(CFG cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));
            return Create(cfg.CreateSCCs());
        }

        /// <summary>
        /// Creates a new loop infos instance.
        /// </summary>
        /// <param name="sccs">The current SCCs.</param>
        /// <returns>The created info instance.</returns>
        public static LoopInfos Create(SCCs sccs)
        {
            if (sccs == null)
                throw new ArgumentNullException(nameof(sccs));
            return new LoopInfos(sccs);
        }

        #endregion

        #region Instance

        private readonly Dictionary<int, LoopInfo> loops;
        private readonly List<LoopInfo> infos;

        /// <summary>
        /// Constructs a new loop infos instance.
        /// </summary>
        /// <param name="sccs">The SCCs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LoopInfos(SCCs sccs)
        {
            Debug.Assert(sccs != null, "Invalid sccs");

            SCCs = sccs;
            loops = new Dictionary<int, LoopInfo>(sccs.Count);
            infos = new List<LoopInfo>(sccs.Count);

            foreach (var scc in sccs)
            {
                // Try to resolve loop info for the current scc.
                if (LoopInfo.TryCreate(scc, out var loopInfo))
                {
                    infos.Add(loopInfo);
                    loops.Add(scc.Index, loopInfo);
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying CFG.
        /// </summary>
        public CFG CFG { get; }

        /// <summary>
        /// Returns the underlying SCCs.
        /// </summary>
        public SCCs SCCs { get; }

        /// <summary>
        /// Returns the number of info objects.
        /// </summary>
        public int Count => infos.Count;

        /// <summary>
        /// Lookups the given loop-info index.
        /// </summary>
        /// <param name="index">The loop-info index.</param>
        /// <returns>The resolved loop info.</returns>
        public LoopInfo this[int index] => infos[index];

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the given node to an associated loop-info instance.
        /// </summary>
        /// <param name="node">The node to map to a loop-info instance.</param>
        /// <param name="loopInfo">The resulting loop info (if any).</param>
        /// <returns>True, if the node could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLoopInfo(CFG.Node node, out LoopInfo loopInfo) =>
            TryGetLoopInfo(node.Block, out loopInfo);

        /// <summary>
        /// Tries to resolve the given block to an associated loop-info instance.
        /// </summary>
        /// <param name="block">The block to map to a loop-info instance.</param>
        /// <param name="loopInfo">The resulting loop info (if any).</param>
        /// <returns>True, if the node could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLoopInfo(BasicBlock block, out LoopInfo loopInfo)
        {
            loopInfo = default;
            return SCCs.TryGetSCC(block, out var scc) &&
                TryGetLoopInfo(scc, out loopInfo);
        }

        /// <summary>
        /// Tries to resolve the given SCC to a loop-info instance.
        /// </summary>
        /// <param name="scc">The SCC to lookup.</param>
        /// <param name="loopInfo">The resolved loop info (if any).</param>
        /// <returns>True, if any loop info could be resolved.</returns>
        public bool TryGetLoopInfo(SCCs.SCC scc, out LoopInfo loopInfo) =>
            loops.TryGetValue(scc.Index, out loopInfo);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates over all infos.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(infos);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<LoopInfo> IEnumerable<LoopInfo>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Represents an operation that works on an induction variable.
    /// </summary>
    /// <typeparam name="T">The operation kind.</typeparam>
    public readonly struct InductionVariableOperation<T>
        where T : struct
    {
        internal InductionVariableOperation(int index, Value value, T kind)
        {
            Debug.Assert(index >= 0, "Invalid index");

            Index = index;
            Value = value;
            Kind = kind;
        }

        /// <summary>
        /// Returns the operand index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the associated constant value.
        /// </summary>
        public Value Value { get; }

        /// <summary>
        /// Returns the kind of the operation.
        /// </summary>
        public T Kind { get; }

        /// <summary>
        /// Returns true if the constant operand value is on the left.
        /// </summary>
        public bool IsLeft => Index == 0;
    }

    /// <summary>
    /// Represents variable bounds of an induction variable.
    /// </summary>
    public readonly struct InductionVariableBounds
    {
        /// <summary>
        /// Constructs a new induction-variable bounds.
        /// </summary>
        /// <param name="init">The initialization value.</param>
        /// <param name="updateOperation">The update operation.</param>
        /// <param name="breakOperation">The break operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal InductionVariableBounds(
            Value init,
            InductionVariableOperation<BinaryArithmeticKind> updateOperation,
            InductionVariableOperation<CompareKind> breakOperation)
        {
            Init = init;
            UpdateOperation = updateOperation;
            BreakOperation = breakOperation;
        }

        /// <summary>
        /// The initialization value.
        /// </summary>
        public Value Init { get; }

        /// <summary>
        /// Returns the update value.
        /// </summary>
        public Value UpdateValue => UpdateOperation.Value;

        /// <summary>
        /// Returns the break value.
        /// </summary>
        public Value BreakValue => BreakOperation.Value;

        /// <summary>
        /// The update kind.
        /// </summary>
        public InductionVariableOperation<BinaryArithmeticKind> UpdateOperation { get; }

        /// <summary>
        /// The break kind.
        /// </summary>
        public InductionVariableOperation<CompareKind> BreakOperation { get; }
    }

    /// <summary>
    /// A single induction variable.
    /// </summary>
    public readonly struct InductionVariable
    {
        #region Instance

        /// <summary>
        /// Constructs a new induction variable.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <param name="phi">The phi node.</param>
        /// <param name="init">The init value.</param>
        /// <param name="update">The update value.</param>
        /// <param name="breakCondition">The break condition.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal InductionVariable(
            int index,
            PhiValue phi,
            ValueReference init,
            ValueReference update,
            ValueReference breakCondition)
        {
            Index = index;
            Phi = phi;
            Init = init;
            Update = update;
            BreakCondition = breakCondition;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the variable index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the associated phi value.
        /// </summary>
        public PhiValue Phi { get; }

        /// <summary>
        /// Returns a link to the init expression.
        /// </summary>
        public Value Init { get; }

        /// <summary>
        /// Returns a link to the update expression.
        /// </summary>
        public Value Update { get; }

        /// <summary>
        /// Returns a link to the break-condition expression.
        /// </summary>
        public Value BreakCondition { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve a known update operation.
        /// </summary>
        /// <param name="updateOperation">The resolved update operation.</param>
        /// <returns>True, if a known operation could be resolved.</returns>
        public bool TryResolveUpdateOperation(
            out InductionVariableOperation<BinaryArithmeticKind> updateOperation)
        {
            updateOperation = default;

            // Check for a primitive operation
            if (!(Update is BinaryArithmeticValue updateValue) ||
                !updateValue.BasicValueType.IsInt())
                return false;

            // Determine the step value
            int stepValueIndex = updateValue.Left.Resolve() == Init ? 1 : 1;
            var resolvedStepValue = updateValue[stepValueIndex].Resolve();

            // Resolve the update operation
            updateOperation = new InductionVariableOperation<BinaryArithmeticKind>(
                stepValueIndex,
                resolvedStepValue,
                updateValue.Kind);
            return true;
        }

        /// <summary>
        /// Tries to resolve a known break operation.
        /// </summary>
        /// <param name="breakOperation">The resolved break operation.</param>
        /// <returns>True, if a known operation could be resolved.</returns>
        public bool TryResolveBreakOperation(
            out InductionVariableOperation<CompareKind> breakOperation)
        {
            breakOperation = default;

            // Check for a primitive operation
            if (!(BreakCondition is CompareValue compareValue) ||
                !compareValue.BasicValueType.IsInt())
                return false;

            int endValueIndex = compareValue.Left.Resolve() == Phi ? 1 : 0;
            var resolvedEndValue = compareValue[endValueIndex].Resolve();

            // Resolve the break operation
            breakOperation = new InductionVariableOperation<CompareKind>(
                endValueIndex,
                resolvedEndValue,
                compareValue.Kind);

            return true;
        }

        /// <summary>
        /// Tries to resolve the related loop bounds.
        /// </summary>
        /// <param name="bounds">The resolved loop bounds (if any).</param>
        /// <returns>True, if the bounds could be resoled.</returns>
        public bool TryResolveBounds(out InductionVariableBounds bounds)
        {
            bounds = default;

            // Try to resolve init and update oeration
            if (!TryResolveUpdateOperation(out var updateOperation) ||
                !TryResolveBreakOperation(out var breakOperation))
                return false;

            bounds = new InductionVariableBounds(
                Init,
                updateOperation,
                breakOperation);

            return true;
        }

        #endregion
    }
}
