// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// A simple loop info object.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public sealed class LoopInfo<TOrder, TDirection>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// A specialized successor provider for loop regions that exclude the exit
        /// block of a particular loop. This emulates a return exit block without any
        /// successors.
        /// </summary>
        private readonly struct ExitSuccessorProvider :
            ITraversalSuccessorsProvider<TDirection>
        {
            #region Instance

            /// <summary>
            /// Constructs a new successor provider.
            /// </summary>
            /// <param name="exitBlock">The exit block.</param>
            public ExitSuccessorProvider(BasicBlock exitBlock)
            {
                ExitBlock = exitBlock;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the unique exit block (the first block that does not belong to
            /// the loop).
            /// </summary>
            public BasicBlock ExitBlock { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns the successors of the given basic block that do not contain the
            /// loop's exit block.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(
                BasicBlock basicBlock) =>
                basicBlock.CurrentSuccessors.ExceptAll(
                    ExitBlock,
                    new BasicBlock.Comparer());

            #endregion
        }

        /// <summary>
        /// A specialized successor provider for loop regions that exclude the exit and
        /// the header blocks of a particular loop. This emulates a return exit block
        /// without any successors.
        /// </summary>
        private readonly struct ExitAndHeaderSuccessorProvider :
            ITraversalSuccessorsProvider<TDirection>
        {
            #region Instance

            private readonly ExitSuccessorProvider exitProvider;

            /// <summary>
            /// Constructs a new successor provider.
            /// </summary>
            /// <param name="exitBlock">The exit block.</param>
            /// <param name="headerBlock">The header block.</param>
            public ExitAndHeaderSuccessorProvider(
                BasicBlock exitBlock,
                BasicBlock headerBlock)
            {
                exitProvider = new ExitSuccessorProvider(exitBlock);
                HeaderBlock = headerBlock;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the unique header block (the first block that belongs to the
            /// loop).
            /// </summary>
            public BasicBlock HeaderBlock { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns the successors of the given basic block that do not contain the
            /// loop's exit block.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(
                BasicBlock basicBlock) =>
                exitProvider.GetSuccessors(basicBlock).ExceptAll(
                    HeaderBlock,
                    new BasicBlock.Comparer());

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new loop info instance from the given SCC while checking for
        /// unique entry and exit blocks.
        /// </summary>
        /// <param name="loop">The SCC.</param>
        /// <returns>The resolved loop info instance.</returns>
        public static LoopInfo<TOrder, TDirection> Create(
            Loops<TOrder, TDirection>.Node loop) =>
            TryCreate(loop, out var loopInfo)
            ? loopInfo
            : throw new InvalidKernelOperationException();

        /// <summary>
        /// Tries to create a new loop info instance from the given SCC while checking
        /// for unique entry and exit blocks.
        /// </summary>
        /// <param name="loop">The SCC.</param>
        /// <param name="loopInfo">The resolved loop info object (if any).</param>
        /// <returns>True, if the resulting loop info object could be resolved.</returns>
        public static bool TryCreate(
            Loops<TOrder, TDirection>.Node loop,
            out LoopInfo<TOrder, TDirection> loopInfo)
        {
            loopInfo = default;

            // Check for simple loops with manageable induction variables
            if (loop.Entries.Length > 1 || loop.Exits.Length > 1 ||
                loop.Headers.Length > 1 || loop.Breakers.Length > 1 ||
                loop.BackEdges.Length > 1 ||
                !TryGetLoopBody(loop, out var body, out bool isDoWhileLoop) ||
                !TryGetPhis(
                    loop,
                    isDoWhileLoop,
                    out var inductionVariables,
                    out var phiValues))
            {
                return false;
            }

            loopInfo = new LoopInfo<TOrder, TDirection>(
                loop,
                body,
                isDoWhileLoop,
                ref inductionVariables,
                ref phiValues);
            return true;
        }

        /// <summary>
        /// Tries to determine a unique body start block.
        /// </summary>
        /// <param name="loop">The parent loop.</param>
        /// <param name="body">The loop body (if any).</param>
        /// <param name="isDoWhileLoop">
        /// True, if the body is executed in all cases.
        /// </param>
        /// <returns>True, if the given loop body could be resolved.</returns>
        private static bool TryGetLoopBody(
            Loops<TOrder, TDirection>.Node loop,
            out BasicBlock body,
            out bool isDoWhileLoop)
        {
            body = null;
            isDoWhileLoop = false;

            // Get the header block and check for supported loop graphs
            var header = loop.Headers[0];
            var successors = header.GetSuccessors<TDirection>();
            if (successors.Length != 2)
                return false;

            // Determine the main body block
            body = loop.Exits.Contains(successors[0]) ? successors[1] : successors[0];

            // Determine whether the body block will be executed in all cases
            isDoWhileLoop = true;
            foreach (var entry in loop.Entries)
            {
                isDoWhileLoop &= entry.Successors.Contains(
                    body,
                    new BasicBlock.Comparer());
            }

            return true;
        }

        /// <summary>
        /// Tries to get all induction variables and supported phi values of the given
        /// loop object.
        /// </summary>
        /// <param name="loop">The parent loop.</param>
        /// <param name="isDoWhileLoop">
        /// True, if the body is executed in all cases.
        /// </param>
        /// <param name="inductionVariables">The list of induction variables.</param>
        /// <param name="phiValues">The list of phi values.</param>
        /// <returns>True, if the given loop has supported phi values.</returns>
        private static bool TryGetPhis(
            Loops<TOrder, TDirection>.Node loop,
            bool isDoWhileLoop,
            out InlineList<InductionVariable> inductionVariables,
            out InlineList<(PhiValue, Value)> phiValues)
        {
            var phis = loop.ComputePhis();
            var visitedPhis = new HashSet<PhiValue>();

            inductionVariables = InlineList<InductionVariable>.Create(phis.Count);
            phiValues = InlineList<(PhiValue, Value)>.Create(phis.Count);

            // Analyze all breakers
            foreach (var breaker in loop.Breakers)
            {
                if (!(breaker.Terminator is ConditionalBranch branch) ||
                    !IsInductionVariable(branch.Condition, out var phiValue) ||
                    !loop.Contains(phiValue.BasicBlock) ||
                    !TryGetPhiOperands(loop, phiValue, out var inside, out var outside))
                {
                    return false;
                }

                inductionVariables.Add(new InductionVariable(
                    inductionVariables.Count,
                    phiValue,
                    outside,
                    inside,
                    branch,
                    isDoWhileLoop));
                visitedPhis.Add(phiValue);
            }

            // Check all phi values
            foreach (var phi in phis)
            {
                // Check whether this phi has already been visited, which also covers
                // caches in which the given phi is an induction variable
                if (!visitedPhis.Add(phi))
                    continue;

                // Check whether this phi value is affected by the current loop; in
                // other words, the phi value receives an argument via the backedge
                if (loop.ContainsBackedgeBlock(phi.Sources))
                {
                    // Try to get the phi operands
                    if (!TryGetPhiOperands(loop, phi, out var _, out var outside))
                        return false;
                    phiValues.Add((phi, outside));
                }
                else if (loop.ConsistsOfBodyBlocks(phi.Sources))
                {
                    // We can safely ignore this phi value since all of its sources
                    // reference loop-internal blocks that will be automatically
                    // remapped during unrolling and do not need to be specialized
                }
                else
                {
                    // We have found a degenerated phi-value can which we cannot unroll
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tries to determine the inside and outside operands of the given phi value.
        /// </summary>
        /// <param name="loop">The parent loop.</param>
        /// <param name="phiValue">The phi value.</param>
        /// <param name="insideOperand">The inside operand (if any).</param>
        /// <param name="outsideOperand">The outside operand (if any).</param>
        /// <returns>True, if both operands could be resolved.</returns>
        private static bool TryGetPhiOperands(
            Loops<TOrder, TDirection>.Node loop,
            PhiValue phiValue,
            out Value insideOperand,
            out Value outsideOperand)
        {
            insideOperand = null;
            outsideOperand = null;

            // Search for two operands of which one is defined
            // outside the current loop and one is from the
            // inside of the current loop
            int numOperands = phiValue.Count;
            if (numOperands != 2)
                return false;

            // Try to get inside and outside operands
            Value firstOperand = phiValue[0];
            Value secondOperand = phiValue[1];
            bool firstContained = loop.Contains(firstOperand.BasicBlock);
            insideOperand = firstContained ? firstOperand : secondOperand;
            outsideOperand = firstContained ? secondOperand : firstOperand;

            return loop.Contains(insideOperand.BasicBlock) &&
                (outsideOperand is PrimitiveValue ||
                !loop.Contains(outsideOperand.BasicBlock));
        }

        /// <summary>
        /// Returns true if the given value is an induction variable.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="phiValue">The resolved induction-variable phi (if any)..</param>
        /// <returns>True, if the given node is an induction-variable branch.</returns>
        private static bool IsInductionVariable(Value value, out PhiValue phiValue)
        {
            phiValue = null;
            if (!(value is CompareValue compareValue))
                return false;

            Value left = compareValue.Left;
            Value right = compareValue.Right;
            phiValue = left as PhiValue;
            return phiValue != null
                ? !(right is PhiValue)
                : (phiValue = right as PhiValue) != null;
        }

        #endregion

        #region Instance

        private InlineList<InductionVariable> inductionVariables;
        private InlineList<(PhiValue, Value)> phiValues;

        /// <summary>
        /// Constructs a new loop info instance.
        /// </summary>
        /// <param name="loop">The parent loop.</param>
        /// <param name="body">The start loop-body block.</param>
        /// <param name="isDoWhileLoop">
        /// True, if the body is executed in all cases.
        /// </param>
        /// <param name="variables">All induction variables.</param>
        /// <param name="values">All affected phi values.</param>
        private LoopInfo(
            in Loops<TOrder, TDirection>.Node loop,
            BasicBlock body,
            bool isDoWhileLoop,
            ref InlineList<InductionVariable> variables,
            ref InlineList<(PhiValue, Value)> values)
        {
            Loop = loop;
            Body = body;
            IsDoWhileLoop = isDoWhileLoop;
            variables.MoveTo(ref inductionVariables);
            values.MoveTo(ref phiValues);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated loop.
        /// </summary>
        public Loops<TOrder, TDirection>.Node Loop { get; }

        /// <summary>
        /// Returns the unique predecessor that does not belong to the loop.
        /// </summary>
        public BasicBlock Entry => Loop.Entries[0];

        /// <summary>
        /// Returns the unique loop header that belongs to the loop.
        /// </summary>
        public BasicBlock Header => Loop.Headers[0];

        /// <summary>
        /// Returns the body entry block.
        /// </summary>
        public BasicBlock Body { get; }

        /// <summary>
        /// Returns true if the body is executed in all cases.
        /// </summary>
        public bool IsDoWhileLoop { get; }

        /// <summary>
        /// Returns the unique exit block that does not belong to the loop.
        /// </summary>
        public BasicBlock Exit => Loop.Exits[0];

        /// <summary>
        /// Returns the unique back-edge block that jumps to the loop header.
        /// </summary>
        public BasicBlock BackEdge => Loop.BackEdges[0];

        /// <summary>
        /// Returns all underlying induction variables.
        /// </summary>
        public ReadOnlySpan<InductionVariable> InductionVariables => inductionVariables;

        /// <summary>
        /// Returns all phi values that are referenced outside of this loop.
        /// </summary>
        public ReadOnlySpan<(PhiValue phi, Value outsideOperand)> PhiValues => phiValues;

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the given block belongs to the associated SCC.
        /// </summary>
        /// <param name="block">The block to map to an SCC.</param>
        /// <returns>True, if the node belongs to the associated SCC.</returns>
        public bool Contains(BasicBlock block) => Loop.Contains(block);

        /// <summary>
        /// Computes an ordered collection of all blocks in this loop starting with the
        /// entry point.
        /// </summary>
        /// <typeparam name="TProvider">The provider implementation.</typeparam>
        /// <param name="entryPoint">The current entry point.</param>
        /// <param name="provider">The provider instance.</param>
        /// <returns>The computed block collection.</returns>
        private BasicBlockCollection<TOrder, TDirection> ComputeOrderedBlocks<TProvider>(
            BasicBlock entryPoint,
            in TProvider provider)
            where TProvider : struct, ITraversalSuccessorsProvider<TDirection> =>
            new TOrder().TraverseToCollection<TOrder, TProvider, TDirection>(
                Loop.Count,
                entryPoint,
                provider);

        /// <summary>
        /// Computes a block ordering of all blocks in this loop.
        /// </summary>
        /// <returns>The computed block ordering.</returns>
        public BasicBlockCollection<TOrder, TDirection> ComputeOrderedBlocks() =>
            ComputeOrderedBlocks(Header, new ExitSuccessorProvider(Exit));

        /// <summary>
        /// Computes a block ordering of all blocks in this loop.
        /// </summary>
        /// <returns>The computed block ordering.</returns>
        public BasicBlockCollection<TOrder, TDirection> ComputeOrderedBodyBlocks() =>
            ComputeOrderedBlocks(Body, new ExitAndHeaderSuccessorProvider(Exit, Header));

        #endregion
    }

    /// <summary>
    /// Encapsulates <see cref="LoopInfo{TOrder, TDirection}"/> instances.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public readonly struct LoopInfos<TOrder, TDirection>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Static

        /// <summary>
        /// Creates a new loop information container.
        /// </summary>
        /// <param name="loops">The source loops to use.</param>
        /// <returns>The created loop information container.</returns>
        public static LoopInfos<TOrder, TDirection> Create(
            Loops<TOrder, TDirection> loops) =>
            new LoopInfos<TOrder, TDirection>(loops);

        #endregion

        #region Instance

        private readonly Dictionary<
            Loops<TOrder, TDirection>.Node,
            LoopInfo<TOrder, TDirection>> mapping;

        /// <summary>
        /// Constructs a new information instance.
        /// </summary>
        /// <param name="loops">The source loops.</param>
        private LoopInfos(Loops<TOrder, TDirection> loops)
        {
            mapping = new Dictionary<
                Loops<TOrder, TDirection>.Node,
                LoopInfo<TOrder, TDirection>>(loops.Count);
            foreach (var loop in loops)
            {
                if (loop.TryGetLoopInfo(out var loopInfo))
                    mapping.Add(loop, loopInfo);
            }
        }

        /// <summary>
        /// Returns the number of loop information objects.
        /// </summary>
        public readonly int Count => mapping.Count;

        /// <summary>
        /// Tries to get previously computed loop information for the given loop.
        /// </summary>
        /// <param name="loop">The loop to get the loop information for.</param>
        /// <param name="value">The resolved loop information (if any).</param>
        /// <returns>True, if a loop information instance could be found.</returns>
        public readonly bool TryGetInfo(
            Loops<TOrder, TDirection>.Node loop,
            out LoopInfo<TOrder, TDirection> value) =>
            mapping.TryGetValue(loop, out value);

        #endregion
    }

    /// <summary>
    /// Helper utility for the class <see cref="LoopInfo{TOrder, TDirection}"/> and the
    /// structure <see cref="LoopInfos{TOrder, TDirection}"/>.
    /// </summary>
    public static class LoopInfos
    {
        /// <summary>
        /// Creates a new loop analysis instance based on the given CFG.
        /// </summary>
        /// <typeparam name="TOrder">The underlying block order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="loop">The underlying loop entry.</param>
        /// <param name="loopInfo">The resolved loop information (if any).</param>
        /// <returns>The created loop analysis.</returns>
        public static bool TryGetLoopInfo<TOrder, TDirection>(
            this Loops<TOrder, TDirection>.Node loop,
            out LoopInfo<TOrder, TDirection> loopInfo)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            LoopInfo<TOrder, TDirection>.TryCreate(loop, out loopInfo);

        /// <summary>
        /// Creates a new aggregated structure containing precomputed loop information.
        /// </summary>
        /// <typeparam name="TOrder">The underlying block order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="loops">The source loops to use.</param>
        /// <returns>The created loop information object.</returns>
        public static LoopInfos<TOrder, TDirection> CreateLoopInfos<TOrder, TDirection>(
            this Loops<TOrder, TDirection> loops)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            LoopInfos<TOrder, TDirection>.Create(loops);
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
        public readonly bool IsLeft => Index == 0;
    }

    /// <summary>
    /// Represents variable bounds of an induction variable.
    /// </summary>
    public readonly struct InductionVariableBounds
    {
        #region Static

        /// <summary>
        /// Tries to map a loop variable to an integer constant.
        /// </summary>
        /// <param name="value">The value to map to an integer bound.</param>
        /// <returns>The mapped integer (if any).</returns>
        private static int? TryGetIntegerBound(Value value) =>
            value is PrimitiveValue primitive && value.BasicValueType.IsInt()
            ? primitive.Int32Value
            : default(int?);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new induction-variable bounds.
        /// </summary>
        /// <param name="init">The initialization value.</param>
        /// <param name="updateOperation">The update operation.</param>
        /// <param name="breakOperation">The break operation.</param>
        /// <param name="isDoWhileLoop">
        /// True, if the current loop is a do-while loop.
        /// </param>
        internal InductionVariableBounds(
            Value init,
            InductionVariableOperation<BinaryArithmeticKind> updateOperation,
            InductionVariableOperation<CompareKind> breakOperation,
            bool isDoWhileLoop)
        {
            Init = init;
            UpdateOperation = updateOperation;
            BreakOperation = breakOperation;
            IsDoWhileLoop = isDoWhileLoop;
        }

        #endregion

        #region Properties

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
        /// Returns true if the current loop is a do-while loop.
        /// </summary>
        public bool IsDoWhileLoop { get; }

        /// <summary>
        /// The update kind.
        /// </summary>
        public InductionVariableOperation<BinaryArithmeticKind> UpdateOperation { get; }

        /// <summary>
        /// The break kind.
        /// </summary>
        public InductionVariableOperation<CompareKind> BreakOperation { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to get integer bounds for all loop variables.
        /// </summary>
        /// <returns>The determined integer-based loop bounds.</returns>
        public readonly (int? init, int? update, int? @break) GetIntegerBounds() =>
            (
                TryGetIntegerBound(Init),
                TryGetIntegerBound(UpdateValue),
                TryGetIntegerBound(BreakValue)
            );

        /// <summary>
        /// Tries to compute the trip count of the loop.
        /// </summary>
        /// <param name="intBounds"></param>
        /// <returns></returns>
        public readonly int? TryGetTripCount(
            out (int? init, int? update, int? @break) intBounds)
        {
            intBounds = GetIntegerBounds();

            // If there are some unknown bounds we cannot determine a simple trip count
            if (!intBounds.init.HasValue ||
                !intBounds.update.HasValue ||
                !intBounds.@break.HasValue)
            {
                return null;
            }

            // Check for unsupported loops
            if (UpdateOperation.Kind != BinaryArithmeticKind.Add &&
                UpdateOperation.Kind != BinaryArithmeticKind.Sub ||
                intBounds.update == 0)
            {
                return null;
            }

            int doWhileOffset = IsDoWhileLoop ? 1 : 0;
            int initVal = intBounds.init.Value;
            int breakVal = intBounds.@break.Value;

            int update = intBounds.update.Value;
            if (UpdateOperation.Kind == BinaryArithmeticKind.Sub)
                update *= -1;

            // Check if a while loop performs at least a single iteration
            bool whileLoopIsEntered;
            switch (BreakOperation.Kind)
            {
                case CompareKind.Equal:
                    whileLoopIsEntered = initVal == breakVal;
                    break;
                case CompareKind.NotEqual:
                    whileLoopIsEntered = initVal != breakVal;
                    break;
                case CompareKind.GreaterEqual:
                    whileLoopIsEntered = initVal >= breakVal;
                    break;
                case CompareKind.GreaterThan:
                    whileLoopIsEntered = initVal > breakVal;
                    break;
                case CompareKind.LessEqual:
                    whileLoopIsEntered = initVal <= breakVal;
                    break;
                case CompareKind.LessThan:
                    whileLoopIsEntered = initVal < breakVal;
                    break;
                default:
                    return null;
            }

            // If a while loop is not entered, it can still be a do-while loop
            if (!whileLoopIsEntered)
                return doWhileOffset;

            // Special Case for CompareKind.Equal: can only be true, once
            if (BreakOperation.Kind == CompareKind.Equal)
                return 1 + doWhileOffset;

            // Determine lastVal (might not be hit exactly)
            int lastVal = BreakOperation.Kind == CompareKind.LessThan ||
                BreakOperation.Kind == CompareKind.GreaterThan ||
                BreakOperation.Kind == CompareKind.NotEqual
                ? update > 0 ? breakVal - 1 : breakVal + 1
                : breakVal;

            // Compute the number of steps
            int stepCount = (lastVal - initVal) / update;

            // If stepCount is less than zero, it's probably an infinite loop
            if (stepCount < 0)
                return null;

            // The trip count is one more than the step count
            return stepCount + 1 + doWhileOffset;
        }

        #endregion
    }

    /// <summary>
    /// A single induction variable.
    /// </summary>
    public sealed class InductionVariable
    {
        #region Instance

        /// <summary>
        /// Constructs a new induction variable.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <param name="phi">The phi node.</param>
        /// <param name="init">The init value.</param>
        /// <param name="update">The update value.</param>
        /// <param name="breakBranch">The branch that breaks the loop.</param>
        /// <param name="isDoWhileLoop">
        /// True, if the current loop is a do-while loop.
        /// </param>
        internal InductionVariable(
            int index,
            PhiValue phi,
            Value init,
            Value update,
            ConditionalBranch breakBranch,
            bool isDoWhileLoop)
        {
            Index = index;
            Phi = phi;
            Init = init;
            Update = update;
            BreakBranch = breakBranch;
            IsDoWhileLoop = isDoWhileLoop;
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
        public Value BreakCondition => BreakBranch.Condition;

        /// <summary>
        /// Returns the branch that actually breaks the loop.
        /// </summary>
        public ConditionalBranch BreakBranch { get; }

        /// <summary>
        /// Returns true if the current loop is a do-while loop.
        /// </summary>
        public bool IsDoWhileLoop { get; }

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
            {
                return false;
            }

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
            {
                return false;
            }

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

            // Try to resolve init and update oration
            if (!TryResolveUpdateOperation(out var updateOperation) ||
                !TryResolveBreakOperation(out var breakOperation))
            {
                return false;
            }

            bounds = new InductionVariableBounds(
                Init,
                updateOperation,
                breakOperation,
                IsDoWhileLoop);
            return true;
        }

        #endregion
    }
}
