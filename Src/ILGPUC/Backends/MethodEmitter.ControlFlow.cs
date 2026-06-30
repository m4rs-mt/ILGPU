// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodEmitter.ControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using static ILGPUC.Backends.StructuredControlFlow;

namespace ILGPUC.Backends;

partial class MethodEmitter
{
    #region Nested Types

    /// <summary>
    /// The kinds of breakable constructs in the emitted code.
    /// </summary>
    private enum ConstructKind
    {
        /// <summary>
        /// A <c>while (true)</c> loop.
        /// </summary>
        Loop,

        /// <summary>
        /// A <c>do { } while (false)</c> region.
        /// </summary>
        Region,

        /// <summary>
        /// A <c>switch</c> statement.
        /// </summary>
        Switch,
    }

    /// <summary>
    /// A breakable construct enclosing the code being emitted.
    /// </summary>
    /// <param name="kind">The construct kind.</param>
    /// <param name="breakTarget">
    /// The jump target reached by a <c>break</c> out of this construct, or null
    /// if leaving the construct does not reach a jump target.
    /// </param>
    /// <param name="header">The loop header, for loops.</param>
    private sealed class Construct(
        ConstructKind kind,
        JumpTarget? breakTarget,
        BasicBlock? header = null)
    {
        public ConstructKind Kind { get; } = kind;
        public JumpTarget? BreakTarget { get; } = breakTarget;
        public BasicBlock? Header { get; } = header;

        /// <summary>
        /// The jump targets of jumps that left this construct through the jump
        /// variable and must be dispatched right after it.
        /// </summary>
        public List<JumpTarget> Pending { get; } = [];
    }

    #endregion

    #region Instance

    private readonly List<Construct> _constructs = [];
    private readonly List<LabeledNode> _labeledNodes = [];
    private readonly Dictionary<JumpTarget, int> _jumpIds = [];
    private CodePlacement? _placement;
    private string _jumpVariable = string.Empty;
    private bool _printing;
    private bool _materialized;
    private bool _usesJumpVariable;

    #endregion

    #region Structured Control Flow

    /// <summary>
    /// Emits the structured statements of a method.
    /// </summary>
    /// <remarks>
    /// The statement tree is walked until the set of regions that need a
    /// breakable construct is stable, and printed afterwards. Jumps that
    /// cannot be expressed by a single <c>break</c> or <c>continue</c> store
    /// their target in a jump variable, leave the innermost construct, and are
    /// dispatched after each construct they leave.
    /// </remarks>
    private void EmitStructuredControlFlow(
        StructuredControlFlow controlFlow,
        CodePlacement placement)
    {
        var tree = controlFlow.BuildTree();
        _placement = placement;
        _jumpIds.Clear();

        _printing = false;
        do
        {
            _materialized = false;
            _usesJumpVariable = false;
            WalkTree(tree);
        }
        while (_materialized);

        if (_usesJumpVariable)
        {
            _jumpVariable = $"_jump{_emitterLocalId++}";
            context.WriteLine($"int {_jumpVariable} = 0;");
        }

        _printing = true;
        WalkTree(tree);
        _printing = false;
        _placement = null;
    }

    /// <summary>
    /// Walks the whole statement tree once.
    /// </summary>
    private void WalkTree(Node tree)
    {
        _constructs.Clear();
        _labeledNodes.Clear();
        Walk(tree, null);
        if (_constructs.Count > 0 || _labeledNodes.Count > 0)
            throw new InvalidOperationException("Unbalanced control-flow constructs");
    }

    /// <summary>
    /// Walks a statement whose end continues at the given jump target.
    /// </summary>
    /// <param name="node">The statement.</param>
    /// <param name="fallthrough">
    /// The jump target reached by falling off the end of the statement, or
    /// null if the end of the statement is unreachable.
    /// </param>
    private void Walk(Node node, JumpTarget? fallthrough)
    {
        switch (node)
        {
            case SequenceNode sequence:
                for (int i = 0, e = sequence.Nodes.Count; i < e; ++i)
                    Walk(sequence.Nodes[i], i + 1 == e ? fallthrough : null);
                break;
            case BlockNode block:
                if (_printing)
                    EmitBasicBlock(block.Block, _placement.AsNotNull());
                break;
            case EdgeNode edge:
                if (_printing)
                    EmitEdgeCopies(edge.Source, edge.Target);
                break;
            case IfNode ifNode:
                WalkIf(ifNode, fallthrough);
                break;
            case SwitchNode switchNode:
                WalkSwitch(switchNode, fallthrough);
                break;
            case LoopNode loop:
                WalkLoop(loop, fallthrough);
                break;
            case LabeledNode labeled:
                WalkLabeled(labeled);
                break;
            case JumpNode jump:
                if (jump.Target != fallthrough)
                    EmitJump(jump.Target);
                break;
        }
    }

    /// <summary>
    /// Walks a two-way branch.
    /// </summary>
    private void WalkIf(IfNode ifNode, JumpTarget? fallthrough)
    {
        if (!_printing)
        {
            Walk(ifNode.TrueNode, fallthrough);
            Walk(ifNode.FalseNode, fallthrough);
            return;
        }

        bool trueEmpty = IsEmpty(ifNode.TrueNode, fallthrough);
        bool falseEmpty = IsEmpty(ifNode.FalseNode, fallthrough);
        if (trueEmpty && falseEmpty)
            return;

        var condition = expressionEmitter.EmitExpression(
            ifNode.Block.TerminationCondition.AsNotNull());
        if (trueEmpty)
        {
            context.WriteLine($"if (!({condition}))");
            EmitScope(ifNode.FalseNode, fallthrough);
            return;
        }

        context.WriteLine($"if ({condition})");
        EmitScope(ifNode.TrueNode, fallthrough);
        if (!falseEmpty)
        {
            context.WriteLine("else");
            EmitScope(ifNode.FalseNode, fallthrough);
        }
    }

    /// <summary>
    /// Emits a statement in its own scope.
    /// </summary>
    private void EmitScope(Node node, JumpTarget? fallthrough)
    {
        context.OpenScope();
        Walk(node, fallthrough);
        context.CloseScope();
    }

    /// <summary>
    /// Walks a multi-way branch. Falling off the end of a case leaves the
    /// switch, which continues at the fallthrough target of the switch.
    /// </summary>
    private void WalkSwitch(SwitchNode switchNode, JumpTarget? fallthrough)
    {
        if (_printing)
        {
            var condition = expressionEmitter.EmitExpression(
                switchNode.Block.TerminationCondition.AsNotNull());
            context.WriteLine($"switch ({condition})");
            context.OpenScope();
        }

        var construct = new Construct(ConstructKind.Switch, fallthrough);
        _constructs.Add(construct);
        foreach (var (values, body) in switchNode.Cases)
        {
            if (_printing)
            {
                foreach (var value in values)
                    context.WriteLine($"case {value}:");
            }
            WalkCase(body, fallthrough);
        }
        if (_printing)
            context.WriteLine("default:");
        WalkCase(switchNode.DefaultNode, fallthrough);
        _constructs.RemoveAt(_constructs.Count - 1);

        if (_printing)
            context.CloseScope();
        Dispatch(construct, fallthrough);
    }

    /// <summary>
    /// Walks the body of a switch case.
    /// </summary>
    private void WalkCase(Node body, JumpTarget? fallthrough)
    {
        if (_printing)
            context.OpenScope();
        Walk(body, fallthrough);
        if (_printing)
        {
            if (!Terminates(body, fallthrough))
                context.WriteLine("break;");
            context.CloseScope();
        }
    }

    /// <summary>
    /// Walks a loop. Falling off the end of the loop body starts the next
    /// iteration; the loop is left by jumps only.
    /// </summary>
    private void WalkLoop(LoopNode loop, JumpTarget? fallthrough)
    {
        if (_printing)
        {
            context.WriteLine("while (true)");
            context.OpenScope();
        }

        var construct = new Construct(ConstructKind.Loop, fallthrough, loop.Header);
        _constructs.Add(construct);
        Walk(loop.Body, new JumpTarget(loop.Header, IsContinue: true));
        _constructs.RemoveAt(_constructs.Count - 1);

        if (_printing)
            context.CloseScope();
        Dispatch(construct, fallthrough);
    }

    /// <summary>
    /// Walks a labeled region, which is immediately followed by the code of its
    /// follow block. Exits that cannot fall off the end of the region use a
    /// <c>do { } while (false)</c> construct to break out of it.
    /// </summary>
    private void WalkLabeled(LabeledNode labeled)
    {
        var exit = new JumpTarget(labeled.Follow, IsContinue: false);
        _labeledNodes.Add(labeled);
        if (!labeled.IsMaterialized)
        {
            Walk(labeled.Body, exit);
            _labeledNodes.RemoveAt(_labeledNodes.Count - 1);
            return;
        }

        if (_printing)
        {
            context.WriteLine("do");
            context.OpenScope();
        }

        var construct = new Construct(ConstructKind.Region, exit);
        _constructs.Add(construct);
        Walk(labeled.Body, exit);
        _constructs.RemoveAt(_constructs.Count - 1);
        _labeledNodes.RemoveAt(_labeledNodes.Count - 1);

        if (_printing)
        {
            context.PopIndent();
            context.WriteLine("} while (false);");
        }
        Dispatch(construct, exit);
    }

    /// <summary>
    /// Emits a jump to the given target that is not reached by falling through.
    /// </summary>
    private void EmitJump(JumpTarget target)
    {
        if (TryEmitDirectJump(target))
            return;

        EnsureReachable(target);
        if (_constructs.Count < 1)
            return;

        _usesJumpVariable = true;
        if (_printing)
        {
            context.WriteLine($"{_jumpVariable} = {GetJumpId(target)};");
            context.WriteLine("break;");
        }
        AddPending(_constructs[^1], target);
    }

    /// <summary>
    /// Tries to emit a jump as a single <c>break</c> or <c>continue</c>.
    /// </summary>
    private bool TryEmitDirectJump(JumpTarget target)
    {
        var statement = GetDirectJump(target);
        if (statement is null)
            return false;
        if (_printing)
            context.WriteLine(statement);
        return true;
    }

    /// <summary>
    /// Returns the single statement reaching the given target from the current
    /// position, or null if there is none.
    /// </summary>
    private string? GetDirectJump(JumpTarget target)
    {
        if (_constructs.Count < 1)
            return null;
        if (_constructs[^1].BreakTarget == target)
            return "break;";
        if (!target.IsContinue)
            return null;

        // A continue passes through switches, but not through regions
        for (int i = _constructs.Count - 1; i >= 0; --i)
        {
            var construct = _constructs[i];
            if (construct.Kind == ConstructKind.Switch)
                continue;
            return construct.Kind == ConstructKind.Loop
                && construct.Header == target.Block
                ? "continue;"
                : null;
        }
        return null;
    }

    /// <summary>
    /// Ensures that an enclosing construct is left at the given target,
    /// materializing the labeled region of a follow block if necessary.
    /// </summary>
    private void EnsureReachable(JumpTarget target)
    {
        foreach (var construct in _constructs)
        {
            if (construct.BreakTarget == target ||
                target.IsContinue && construct.Header == target.Block)
            {
                return;
            }
        }

        if (!target.IsContinue)
        {
            foreach (var labeled in _labeledNodes)
            {
                if (labeled.Follow != target.Block)
                    continue;
                // Only possible while resolving: printing uses the final set
                // of materialized regions
                labeled.IsMaterialized = true;
                _materialized = true;
                return;
            }
        }

        // Jumps that left a region materialized during this walk are resolved
        // in the next walk
        if (_materialized && !_printing)
            return;

        throw new InvalidOperationException(
            $"Jump target {target.Block.ToReferenceString()} is not reachable");
    }

    /// <summary>
    /// Dispatches the pending jumps that left the given construct.
    /// </summary>
    /// <param name="construct">The construct that has just been closed.</param>
    /// <param name="fallthrough">
    /// The jump target reached by falling off the end of the construct.
    /// </param>
    private void Dispatch(Construct construct, JumpTarget? fallthrough)
    {
        bool propagate = false;
        foreach (var target in construct.Pending)
        {
            var id = GetJumpId(target);
            if (target == fallthrough)
            {
                if (_printing)
                {
                    context.WriteLine(
                        $"if ({_jumpVariable} == {id}) {_jumpVariable} = 0;");
                }
                continue;
            }

            var statement = GetDirectJump(target);
            if (statement is not null)
            {
                if (_printing)
                {
                    context.WriteLine($"if ({_jumpVariable} == {id})");
                    context.OpenScope();
                    context.WriteLine($"{_jumpVariable} = 0;");
                    context.WriteLine(statement);
                    context.CloseScope();
                }
                continue;
            }

            EnsureReachable(target);
            if (_constructs.Count > 0)
                AddPending(_constructs[^1], target);
            propagate = true;
        }

        if (propagate && _printing)
            context.WriteLine($"if ({_jumpVariable} != 0) break;");
    }

    /// <summary>
    /// Registers a pending jump target with a construct.
    /// </summary>
    private static void AddPending(Construct construct, JumpTarget target)
    {
        if (!construct.Pending.Contains(target))
            construct.Pending.Add(target);
    }

    /// <summary>
    /// Returns the jump-variable value identifying the given target.
    /// </summary>
    private int GetJumpId(JumpTarget target)
    {
        if (!_jumpIds.TryGetValue(target, out int id))
        {
            id = _jumpIds.Count + 1;
            _jumpIds.Add(target, id);
        }
        return id;
    }

    /// <summary>
    /// Returns true if the given statement emits no code.
    /// </summary>
    private static bool IsEmpty(Node node, JumpTarget? fallthrough)
    {
        switch (node)
        {
            case SequenceNode sequence:
                for (int i = 0, e = sequence.Nodes.Count; i < e; ++i)
                {
                    if (!IsEmpty(sequence.Nodes[i], i + 1 == e ? fallthrough : null))
                        return false;
                }
                return true;
            case EdgeNode edge:
                return GetEdgeCopies(edge.Source, edge.Target).Count < 1;
            case IfNode ifNode:
                return IsEmpty(ifNode.TrueNode, fallthrough)
                    && IsEmpty(ifNode.FalseNode, fallthrough);
            case LabeledNode labeled:
                return !labeled.IsMaterialized && IsEmpty(
                    labeled.Body,
                    new JumpTarget(labeled.Follow, IsContinue: false));
            case JumpNode jump:
                return jump.Target == fallthrough;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns true if control never falls off the end of the given statement.
    /// </summary>
    private static bool Terminates(Node node, JumpTarget? fallthrough) =>
        node switch
        {
            SequenceNode sequence => sequence.Nodes.Count > 0
                && Terminates(sequence.Nodes[^1], fallthrough),
            BlockNode block =>
                block.Block.TerminationKind == BlockTerminationKind.Return,
            IfNode ifNode => Terminates(ifNode.TrueNode, fallthrough)
                && Terminates(ifNode.FalseNode, fallthrough),
            JumpNode jump => jump.Target != fallthrough,
            _ => false
        };

    #endregion

    #region Phi Copies

    /// <summary>
    /// Returns the phi assignments of the edge from source to target.
    /// </summary>
    private static List<(PhiValue Phi, Value Argument)> GetEdgeCopies(
        BasicBlock source,
        BasicBlock target)
    {
        var copies = new List<(PhiValue, Value)>();
        foreach (var phi in target.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Sources[i] != source)
                    continue;
                var argument = phi.Arguments[i];
                if (argument != phi)
                    copies.Add((phi, argument));
                break;
            }
        }
        return copies;
    }

    /// <summary>
    /// Emits the phi assignments of the edge from source to target as a
    /// parallel copy: every argument is read before any phi is assigned.
    /// Assignments whose phi is not read by another pending argument are
    /// emitted first; the remaining cyclic assignments go through
    /// temporaries.
    /// </summary>
    private void EmitEdgeCopies(BasicBlock source, BasicBlock target)
    {
        var pending = GetEdgeCopies(source, target);
        var phis = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        bool progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            for (int i = 0; i < pending.Count; i++)
            {
                var (phi, argument) = pending[i];
                phis.Clear();
                phis.Add(phi);
                bool isRead = false;
                for (int j = 0; j < pending.Count && !isRead; j++)
                    isRead = j != i && ReadsAny(pending[j].Argument, phis);
                if (isRead)
                    continue;

                var incoming = expressionEmitter.EmitExpression(argument);
                context.WriteLine($"{context.GetValueName(phi)} = {incoming};");
                pending.RemoveAt(i--);
                progress = true;
            }
        }
        if (pending.Count < 1)
            return;

        context.OpenScope();
        var temporaries = new string[pending.Count];
        for (int i = 0; i < pending.Count; i++)
        {
            var (phi, argument) = pending[i];
            temporaries[i] = $"_phi{_emitterLocalId++}";
            var typeName = typeEmitter.GetTypeName(phi.Type);
            var incoming = expressionEmitter.EmitExpression(argument);
            context.WriteLine($"{typeName} {temporaries[i]} = {incoming};");
        }
        for (int i = 0; i < pending.Count; i++)
        {
            var phiName = context.GetValueName(pending[i].Phi);
            context.WriteLine($"{phiName} = {temporaries[i]};");
        }
        context.CloseScope();
    }

    /// <summary>
    /// Returns true if the emitted expression of the given value reads one of
    /// the given phi variables. Values with their own variable are read from
    /// that variable and do not read their operands.
    /// </summary>
    private bool ReadsAny(Value value, HashSet<Value> phis)
    {
        if (value is PhiValue)
            return phis.Contains(value);
        if (value is not PureValue || context.NeedsVariable(value))
            return false;
        foreach (var operand in value.Values)
        {
            if (ReadsAny(operand, phis))
                return true;
        }
        return false;
    }

    #endregion

    #region State Machine

    /// <summary>
    /// Emits a method with irreducible control flow as a state machine that
    /// dispatches on the current block in a loop.
    /// </summary>
    private void EmitStateMachine(Method method, CodePlacement placement)
    {
        var stateIds = new Dictionary<BasicBlock, int>();
        foreach (var block in method.Blocks)
            stateIds[block] = stateIds.Count;

        var stateVariable = $"_state{_emitterLocalId++}";
        context.WriteLine($"int {stateVariable} = {stateIds[method.EntryBlock]};");
        context.WriteLine("while (true)");
        context.OpenScope();
        context.WriteLine($"switch ({stateVariable})");
        context.OpenScope();

        foreach (var block in method.Blocks)
        {
            context.WriteLine($"case {stateIds[block]}:");
            context.OpenScope();
            EmitBasicBlock(block, placement);
            EmitStateTransitions(block, stateVariable, stateIds);
            context.CloseScope();
        }

        context.CloseScope(); // switch
        context.CloseScope(); // while
    }

    /// <summary>
    /// Emits the transitions of a block within a state machine.
    /// </summary>
    private void EmitStateTransitions(
        BasicBlock block,
        string stateVariable,
        Dictionary<BasicBlock, int> stateIds)
    {
        void EmitTransition(BasicBlock target)
        {
            EmitEdgeCopies(block, target);
            context.WriteLine($"{stateVariable} = {stateIds[target]};");
        }

        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                EmitTransition(block.Successors[0]);
                break;
            case BlockTerminationKind.Conditional:
                {
                    var view = block.AsConditionalView();
                    var condition = expressionEmitter.EmitExpression(view.Condition);
                    context.WriteLine($"if ({condition})");
                    context.OpenScope();
                    EmitTransition(view.TrueTarget);
                    context.CloseScope();
                    context.WriteLine("else");
                    context.OpenScope();
                    EmitTransition(view.FalseTarget);
                    context.CloseScope();
                    break;
                }
            case BlockTerminationKind.Switch:
                {
                    var view = block.AsSwitchView();
                    var condition = expressionEmitter.EmitExpression(view.Condition);
                    context.WriteLine($"switch ({condition})");
                    context.OpenScope();
                    for (int i = 0; i < view.NumCases; i++)
                    {
                        context.WriteLine($"case {i}:");
                        context.PushIndent();
                        EmitTransition(view.GetCaseTarget(i));
                        context.WriteLine("break;");
                        context.PopIndent();
                    }
                    context.WriteLine("default:");
                    context.PushIndent();
                    EmitTransition(view.DefaultTarget);
                    context.WriteLine("break;");
                    context.PopIndent();
                    context.CloseScope();
                    break;
                }
            case BlockTerminationKind.Return:
                // The return statement is part of the block
                return;
        }
        context.WriteLine("break;");
    }

    #endregion
}
