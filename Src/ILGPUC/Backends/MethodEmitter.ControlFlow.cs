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
        /// A <c>while (true)</c>, <c>while (cond)</c> or
        /// <c>do { } while (cond)</c> loop.
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
    private IfNode? _latchTest;

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
            case IfNode ifNode when ifNode == _latchTest:
                // Emitted as the condition of the enclosing do-while loop
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
            if (!NeverFallsThrough(body, fallthrough))
                context.WriteLine("break;");
            context.CloseScope();
        }
    }

    /// <summary>
    /// Walks a loop in the most structured form its shape allows.
    /// </summary>
    private void WalkLoop(LoopNode loop, JumpTarget? fallthrough)
    {
        // Code on the exit edge of the test runs after the loop, where other
        // exits would have to skip it through the jump variable: such loops
        // are better off with breaks.
        //
        // A header that is also the latch is a do-while loop: as a while loop,
        // its values would be computed in front of the body and again after
        // the loop.
        bool isSelfLoop = loop.LatchTest?.Branch.Block == loop.Header;
        if (loop.HeaderTest is { } headerTest
            && !isSelfLoop
            && IsTestOnly(loop.Header)
            && (!loop.HasOtherExits(headerTest) || !HasExitCode(loop, headerTest)))
        {
            WalkWhileLoop(loop, headerTest, fallthrough);
        }
        else if (loop.LatchTest is { } latchTest
            && TryOrderCopies(latchTest.Branch.Block, loop.Header, out var copies)
            && (!loop.HasOtherExits(latchTest) || !HasExitCode(loop, latchTest)))
        {
            WalkDoWhileLoop(loop, latchTest, copies, fallthrough);
        }
        else
            WalkEndlessLoop(loop, fallthrough);
    }

    /// <summary>
    /// Walks a loop as <c>while (true)</c>. Falling off the end of the loop
    /// body starts the next iteration; the loop is left by jumps only.
    /// </summary>
    private void WalkEndlessLoop(LoopNode loop, JumpTarget? fallthrough)
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
    /// Walks a loop whose header only computes the exit test as
    /// <c>while (cond) { ... }</c>. Header values the test depends on are
    /// computed in the condition, all other header values at the start of
    /// the body and, if the code after the loop needs them, after the loop.
    /// </summary>
    private void WalkWhileLoop(
        LoopNode loop,
        LoopTest test,
        JumpTarget? fallthrough)
    {
        var header = loop.Header;
        var condition = header.TerminationCondition.AsNotNull();
        var values = GetHeaderAssignments(header);

        // Determine the values the test depends on
        var needed = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        var reads = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        for (int i = values.Count - 1; i >= 0; --i)
        {
            reads.Clear();
            reads.Add(values[i]);
            bool isNeeded = ReadsAny(condition, reads);
            foreach (var other in needed)
                isNeeded |= ReadsAnyOperand(other, reads);
            if (isNeeded)
                needed.Add(values[i]);
        }

        // Determine the values to compute again after the loop, since the
        // body is skipped when the test leaves the loop: values used after the
        // loop or by the phi copies of the exit edge, and every other body
        // value they read, as those still hold the previous iteration. The
        // header stores nothing, so loads read the same memory again
        var exitJump = GetExitJump(test);
        var exitCopies = GetEdgeCopies(header, exitJump.Block);
        var exits = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        for (int i = values.Count - 1; i >= 0; --i)
        {
            var value = values[i];
            if (needed.Contains(value))
                continue;
            reads.Clear();
            reads.Add(value);
            bool isExit = IsUsedOutside(value, loop.Members);
            foreach (var (_, argument) in exitCopies)
                isExit |= ReadsAny(argument, reads);
            foreach (var other in exits)
                isExit |= ReadsAnyOperand(other, reads);
            if (isExit)
                exits.Add(value);
        }

        var bodyValues = new List<Value>();
        var exitValues = new List<Value>();
        foreach (var value in values)
        {
            if (needed.Contains(value))
                continue;
            bodyValues.Add(value);
            if (exits.Contains(value))
                exitValues.Add(value);
        }

        // A break lands in front of the exit code, which may only be skipped
        // if it consists of the exit jump alone
        bool exitHasCode = exitValues.Count > 0 || exitCopies.Count > 0;
        JumpTarget? after = exitHasCode ? null : exitJump;

        if (_printing)
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                if (needed.Contains(value))
                    parts.Add(GetAssignment(value));
            }
            var check = expressionEmitter.EmitExpression(condition);
            parts.Add(test.ExitsOnTrue ? $"!({check})" : check);
            context.WriteLine($"while ({string.Join(", ", parts)})");
            context.OpenScope();
            foreach (var value in bodyValues)
                context.WriteLine(GetAssignment(value) + ";");
        }

        var construct = new Construct(ConstructKind.Loop, after, header);
        _constructs.Add(construct);
        Walk(test.LoopNode, new JumpTarget(header, IsContinue: true));
        _constructs.RemoveAt(_constructs.Count - 1);

        if (_printing)
            context.CloseScope();
        Dispatch(construct, after);

        if (_printing)
        {
            foreach (var value in exitValues)
                context.WriteLine(GetAssignment(value) + ";");
        }
        Walk(test.ExitNode, fallthrough);
    }

    /// <summary>
    /// Walks a loop whose only back edge is a branch at the end of its body as
    /// <c>do { ... } while (cond);</c>. The phi copies of the back edge are
    /// part of the condition and run only if the loop continues, so that the
    /// exit sees the values of the last iteration.
    /// </summary>
    private void WalkDoWhileLoop(
        LoopNode loop,
        LoopTest test,
        List<(PhiValue Phi, Value Argument)> copies,
        JumpTarget? fallthrough)
    {
        var latch = test.Branch.Block;
        var exitJump = GetExitJump(test);
        JumpTarget? after = GetEdgeCopies(latch, exitJump.Block).Count > 0
            ? null
            : exitJump;

        if (_printing)
        {
            context.WriteLine("do");
            context.OpenScope();
        }

        // Without a header, a continue would jump to the condition: all jumps
        // to the next iteration use the condition instead
        var construct = new Construct(ConstructKind.Loop, after);
        _constructs.Add(construct);
        var previousTest = _latchTest;
        _latchTest = test.Branch;
        Walk(loop.Body, null);
        _latchTest = previousTest;
        _constructs.RemoveAt(_constructs.Count - 1);

        if (_printing)
        {
            var condition = expressionEmitter.EmitExpression(
                latch.TerminationCondition.AsNotNull());
            if (test.ExitsOnTrue)
                condition = $"!({condition})";
            if (copies.Count > 0)
            {
                var assignments = new List<string>(copies.Count + 1);
                foreach (var (phi, argument) in copies)
                {
                    var incoming = expressionEmitter.EmitExpression(argument);
                    assignments.Add($"{context.GetValueName(phi)} = {incoming}");
                }
                assignments.Add("true");
                condition = test.ExitsOnTrue
                    ? $"{condition} && ({string.Join(", ", assignments)})"
                    : $"({condition}) && ({string.Join(", ", assignments)})";
            }
            context.PopIndent();
            context.WriteLine($"}} while ({condition});");
        }
        Dispatch(construct, after);
        Walk(test.ExitNode, fallthrough);
    }

    /// <summary>
    /// Returns true if leaving a loop through the given test runs code after
    /// the loop: phi copies of the exit edge or, for a header test, header
    /// values used after the loop.
    /// </summary>
    private bool HasExitCode(LoopNode loop, LoopTest test)
    {
        var block = test.Branch.Block;
        if (GetEdgeCopies(block, GetExitJump(test).Block).Count > 0)
            return true;
        if (block != loop.Header || !IsTestOnly(block))
            return false;
        foreach (var value in GetHeaderAssignments(block))
        {
            if (IsUsedOutside(value, loop.Members))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the jump target of the exit side of a loop test.
    /// </summary>
    private static JumpTarget GetExitJump(LoopTest test) =>
        ((JumpNode)((SequenceNode)test.ExitNode).Nodes[^1]).Target;

    /// <summary>
    /// Returns true if the emitted code of the given block consists of
    /// assignments that can be evaluated as part of a loop condition.
    /// </summary>
    private bool IsTestOnly(BasicBlock block)
    {
        foreach (var value in _placement.AsNotNull().GetPlacedBlock(block))
        {
            switch (value)
            {
                case PhiValue:
                    continue;
                case SetField when context.NeedsVariable(value):
                    return false;
                case PureValue:
                    continue;
                case Load when context.NeedsVariable(value):
                    continue;
                case BasicBlockValue bbv when bbv.Type is VoidType
                    || !context.NeedsVariable(bbv):
                    if (!string.IsNullOrEmpty(expressionEmitter.EmitStatement(bbv)))
                        return false;
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns the values of a test-only block that are assigned to
    /// variables, in emission order.
    /// </summary>
    private List<Value> GetHeaderAssignments(BasicBlock block)
    {
        var values = new List<Value>();
        foreach (var value in _placement.AsNotNull().GetPlacedBlock(block))
        {
            if (value is PureValue or Load
                && value.Type is not VoidType
                && context.NeedsVariable(value)
                && !values.Contains(value))
            {
                values.Add(value);
            }
        }
        return values;
    }

    /// <summary>
    /// Returns the assignment expression of a value with a variable.
    /// </summary>
    private string GetAssignment(Value value) =>
        $"{context.GetValueName(value)} = {expressionEmitter.EmitDefinition(value)}";

    /// <summary>
    /// Returns true if the given value is used outside the given loop.
    /// </summary>
    private static bool IsUsedOutside(Value value, HashSet<BasicBlock> members)
    {
        var visited = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Value>();
        pending.Push(value);
        while (pending.Count > 0)
        {
            foreach (var use in pending.Pop().Uses)
            {
                switch (use.Target)
                {
                    case BasicBlock block when !members.Contains(block):
                        return true;
                    case PhiValue phi when use.Index >= 0 && use.Index < phi.NumArguments:
                        // The argument is read on the incoming edge
                        if (!members.Contains(phi.Sources[use.Index]))
                            return true;
                        break;
                    case BasicBlockValue user when !members.Contains(user.BasicBlock):
                        return true;
                    case PureValue user when visited.Add(user):
                        pending.Push(user);
                        break;
                }
            }
        }
        return false;
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
    private bool IsEmpty(Node node, JumpTarget? fallthrough)
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
            case BlockNode block:
                return EmitsNoCode(block.Block);
            case EdgeNode edge:
                return GetEdgeCopies(edge.Source, edge.Target).Count < 1;
            case IfNode ifNode when ifNode == _latchTest:
                return true;
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
    /// Returns true if <see cref="EmitBasicBlock"/> emits nothing for the
    /// given block: it does not return and all of its values are inlined
    /// into their uses.
    /// </summary>
    private bool EmitsNoCode(BasicBlock block)
    {
        if (block.TerminationKind == BlockTerminationKind.Return)
            return false;
        foreach (var value in _placement.AsNotNull().GetPlacedBlock(block))
        {
            switch (value)
            {
                case PhiValue:
                    continue;
                case PureValue when !context.NeedsVariable(value):
                    continue;
                case BasicBlockValue bbv and not Alloca
                    when bbv.Type is VoidType || !context.NeedsVariable(bbv):
                    if (!string.IsNullOrEmpty(expressionEmitter.EmitStatement(bbv)))
                        return false;
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns true if control never falls off the end of the emitted code of
    /// the given statement. A jump to the fallthrough target emits nothing
    /// and falls off the end.
    /// </summary>
    private bool NeverFallsThrough(Node node, JumpTarget? fallthrough) =>
        node switch
        {
            IfNode ifNode when ifNode == _latchTest => false,
            SequenceNode sequence => sequence.Nodes.Count > 0
                && NeverFallsThrough(sequence.Nodes[^1], fallthrough),
            BlockNode block =>
                block.Block.TerminationKind == BlockTerminationKind.Return,
            IfNode ifNode => NeverFallsThrough(ifNode.TrueNode, fallthrough)
                && NeverFallsThrough(ifNode.FalseNode, fallthrough),
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
    /// Moves the phi assignments of an edge whose phi is not read by another
    /// pending argument from the pending list to the ordered list, until only
    /// cyclic assignments remain. Emitting the ordered assignments in order
    /// reads every argument before its value is overwritten.
    /// </summary>
    private void OrderCopies(
        List<(PhiValue Phi, Value Argument)> pending,
        List<(PhiValue Phi, Value Argument)> ordered)
    {
        var phis = new HashSet<Value>(ReferenceEqualityComparer.Instance);
        bool progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            for (int i = 0; i < pending.Count; i++)
            {
                phis.Clear();
                phis.Add(pending[i].Phi);
                bool isRead = false;
                for (int j = 0; j < pending.Count && !isRead; j++)
                    isRead = j != i && ReadsAny(pending[j].Argument, phis);
                if (isRead)
                    continue;

                ordered.Add(pending[i]);
                pending.RemoveAt(i--);
                progress = true;
            }
        }
    }

    /// <summary>
    /// Returns the phi assignments of an edge in an order that needs no
    /// temporaries, if there is one.
    /// </summary>
    private bool TryOrderCopies(
        BasicBlock source,
        BasicBlock target,
        out List<(PhiValue Phi, Value Argument)> ordered)
    {
        var pending = GetEdgeCopies(source, target);
        ordered = new(pending.Count);
        OrderCopies(pending, ordered);
        return pending.Count < 1;
    }

    /// <summary>
    /// Emits the phi assignments of the edge from source to target as a
    /// parallel copy: every argument is read before any phi is assigned.
    /// Cyclic assignments go through temporaries.
    /// </summary>
    private void EmitEdgeCopies(BasicBlock source, BasicBlock target)
    {
        var pending = GetEdgeCopies(source, target);
        var ordered = new List<(PhiValue Phi, Value Argument)>(pending.Count);
        OrderCopies(pending, ordered);
        foreach (var (phi, argument) in ordered)
        {
            var incoming = expressionEmitter.EmitExpression(argument);
            context.WriteLine($"{context.GetValueName(phi)} = {incoming};");
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
    /// Returns true if the emitted expression of the given value reads the
    /// variable of one of the given values. Values with their own variable
    /// are read from that variable and do not read their operands.
    /// </summary>
    private bool ReadsAny(Value value, HashSet<Value> variables)
    {
        if (variables.Contains(value))
            return true;
        if (value is not PureValue || context.NeedsVariable(value))
            return false;
        return ReadsAnyOperand(value, variables);
    }

    /// <summary>
    /// Returns true if the defining expression of the given value reads the
    /// variable of one of the given values.
    /// </summary>
    private bool ReadsAnyOperand(Value value, HashSet<Value> variables)
    {
        foreach (var operand in value.Values)
        {
            if (ReadsAny(operand, variables))
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
