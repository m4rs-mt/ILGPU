// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructuredControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;
using PreDominators = ILGPUC.IR.Analyses.Dominators<ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.Backends;

/// <summary>
/// Translates the control-flow graph of a method into a tree of structured
/// statements (loops, conditionals, switches and forward jumps) that preserves
/// every edge of the graph.
/// </summary>
/// <remarks>
/// The translation follows the dominator tree of the method: every block is
/// emitted exactly once, either inline at its only forward incoming edge or
/// as a follow block placed after the code of its owner (its immediate
/// dominator, or the header of the outermost loop it exits). Forward edges to
/// follow blocks become exits of a labeled region that ends right before the
/// follow block; edges to loop headers become continues. Every path through
/// the tree therefore ends in an explicit return or jump, and the phi copies
/// of each edge are emitted on exactly that edge. Irreducible graphs cannot
/// be translated and are reported through <see cref="IsReducible"/>.
///
/// Empty forwarding blocks (no values, no phis, an unconditional branch) are
/// transparent: an edge to a forwarding block continues with the edge to its
/// successor, so they never become follow blocks of their own.
/// </remarks>
sealed class StructuredControlFlow
{
    #region Nested Types

    /// <summary>
    /// The base class of all structured statements.
    /// </summary>
    internal abstract class Node;

    /// <summary>
    /// A sequence of statements.
    /// </summary>
    internal sealed class SequenceNode(List<Node> nodes) : Node
    {
        public List<Node> Nodes { get; } = nodes;
    }

    /// <summary>
    /// The values of a block, including its return statement.
    /// </summary>
    internal sealed class BlockNode(BasicBlock block) : Node
    {
        public BasicBlock Block { get; } = block;
    }

    /// <summary>
    /// The phi copies of the edge from source to target.
    /// </summary>
    internal sealed class EdgeNode(BasicBlock source, BasicBlock target) : Node
    {
        public BasicBlock Source { get; } = source;
        public BasicBlock Target { get; } = target;
    }

    /// <summary>
    /// A two-way branch on the condition of a block.
    /// </summary>
    internal sealed class IfNode(BasicBlock block, Node trueNode, Node falseNode)
        : Node
    {
        public BasicBlock Block { get; } = block;
        public Node TrueNode { get; } = trueNode;
        public Node FalseNode { get; } = falseNode;
    }

    /// <summary>
    /// A multi-way branch on the condition of a block. Cases with the same
    /// target share one body; cases branching to the default target are
    /// folded into the default body.
    /// </summary>
    internal sealed class SwitchNode(
        BasicBlock block,
        List<(List<int> Values, Node Body)> cases,
        Node defaultNode) : Node
    {
        public BasicBlock Block { get; } = block;
        public List<(List<int> Values, Node Body)> Cases { get; } = cases;
        public Node DefaultNode { get; } = defaultNode;
    }

    /// <summary>
    /// A loop headed by the given block whose body starts with the header.
    /// Falling off the end of the body starts the next iteration; the loop is
    /// left by jumps. The emitter may rebuild it as a <c>while</c> or
    /// <c>do</c>-<c>while</c> loop through its <see cref="HeaderTest"/> or
    /// <see cref="LatchTest"/>.
    /// </summary>
    internal sealed class LoopNode(
        BasicBlock header,
        HashSet<BasicBlock> members,
        Node body) : Node
    {
        public BasicBlock Header { get; } = header;
        public HashSet<BasicBlock> Members { get; } = members;
        public Node Body { get; } = body;

        /// <summary>
        /// The branch of the header if the header branch leaves the loop on
        /// one side: the loop may be emitted as <c>while (cond) { ... }</c>.
        /// </summary>
        /// <remarks>
        /// The rule is deliberately conservative: it is only set if the
        /// header owns no follow blocks inside the loop (so that the body is
        /// exactly the code of the header) and the header code consists of
        /// its values followed by the branch (see <c>GetLoopTest</c>).
        /// </remarks>
        public LoopTest? HeaderTest { get; set; }

        /// <summary>
        /// The branch of the only back edge if it is the last statement of the
        /// body and leaves the loop on its other side: the loop may be emitted
        /// as <c>do { ... } while (cond);</c>.
        /// </summary>
        public LoopTest? LatchTest { get; set; }

        /// <summary>
        /// Returns true if the loop is left by an edge other than the exit
        /// edge of the given test.
        /// </summary>
        public bool HasOtherExits(LoopTest test)
        {
            var exit = ((SequenceNode)test.ExitNode).Nodes[0] as EdgeNode;
            foreach (var member in Members)
            {
                foreach (var successor in member.Successors)
                {
                    if (Members.Contains(successor))
                        continue;
                    if (member != exit?.Source || successor != exit.Target)
                        return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// A two-way branch of a loop that leaves the loop on one side.
    /// </summary>
    /// <param name="Branch">The branch.</param>
    /// <param name="ExitsOnTrue">True if the true side leaves the loop.</param>
    internal readonly record struct LoopTest(IfNode Branch, bool ExitsOnTrue)
    {
        /// <summary>
        /// Returns the side staying in the loop.
        /// </summary>
        public Node LoopNode => ExitsOnTrue ? Branch.FalseNode : Branch.TrueNode;

        /// <summary>
        /// Returns the side leaving the loop: the phi copies of the exit edge
        /// followed by the jump to the exit block.
        /// </summary>
        public Node ExitNode => ExitsOnTrue ? Branch.TrueNode : Branch.FalseNode;
    }

    /// <summary>
    /// A region whose exit continues with the code of the given follow block,
    /// which is emitted right after the region.
    /// </summary>
    internal sealed class LabeledNode(BasicBlock follow, Node body) : Node
    {
        public BasicBlock Follow { get; } = follow;
        public Node Body { get; } = body;

        /// <summary>
        /// True if exits from within the body need a breakable construct,
        /// because they cannot be expressed by falling out of the body.
        /// </summary>
        public bool IsMaterialized { get; set; }
    }

    /// <summary>
    /// A jump to the given target.
    /// </summary>
    internal sealed class JumpNode(JumpTarget target) : Node
    {
        public JumpTarget Target { get; } = target;
    }

    #endregion

    #region Static

    /// <summary>
    /// Analyzes the control flow of the given method.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="placement">The code placement of the method.</param>
    public static StructuredControlFlow Create(
        Method method,
        CodePlacement placement) =>
        new(method, placement);

    #endregion

    #region Instance

    private readonly Method _method;
    private readonly CodePlacement _placement;
    private readonly PreDominators _dominators;
    private readonly Dictionary<BasicBlock, int> _order = [];
    private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> _loops = [];
    private readonly HashSet<BasicBlock> _forwarding = [];
    private readonly HashSet<BasicBlock> _followBlocks = [];
    private readonly Dictionary<BasicBlock, List<BasicBlock>> _ownedFollowBlocks = [];

    private StructuredControlFlow(Method method, CodePlacement placement)
    {
        _method = method;
        _placement = placement;
        _dominators = placement.Dominators;

        foreach (var block in method.Blocks)
            _order[block] = _order.Count;

        IsReducible = ComputeLoops();
        if (IsReducible)
        {
            ComputeForwardingBlocks();
            ComputeFollowBlocks();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if every retreating edge targets a block dominating its
    /// source, i.e. the graph can be translated into structured statements.
    /// </summary>
    public bool IsReducible { get; }

    #endregion

    #region Analysis

    /// <summary>
    /// Returns true if the given edge targets a loop header.
    /// </summary>
    private bool IsBackEdge(BasicBlock source, BasicBlock target) =>
        _order[target] <= _order[source];

    /// <summary>
    /// Returns the targets of the branches of the given block in the tree:
    /// the cases of a switch with the same target share one body, while the
    /// two sides of a conditional branch are separate statements even if
    /// they have the same target.
    /// </summary>
    private static IEnumerable<BasicBlock> GetBranchTargets(BasicBlock block)
    {
        var successors = block.Successors.ToArray();
        if (block.TerminationKind != BlockTerminationKind.Switch)
            return successors;
        var targets = new List<BasicBlock>(successors.Length);
        foreach (var successor in successors)
        {
            if (!targets.Contains(successor))
                targets.Add(successor);
        }
        return targets;
    }

    /// <summary>
    /// Computes the natural loop of every loop header.
    /// </summary>
    /// <returns>True if the control-flow graph is reducible.</returns>
    private bool ComputeLoops()
    {
        foreach (var block in _method.Blocks)
        {
            foreach (var successor in block.Successors)
            {
                if (!IsBackEdge(block, successor))
                    continue;
                if (!_dominators.Dominates(successor, block))
                    return false;

                if (!_loops.TryGetValue(successor, out var members))
                {
                    members = [successor];
                    _loops.Add(successor, members);
                }

                // Collect all blocks reaching the latch without passing
                // through the header
                var pending = new Stack<BasicBlock>();
                if (members.Add(block))
                    pending.Push(block);
                while (pending.Count > 0)
                {
                    foreach (var predecessor in pending.Pop().Predecessors)
                    {
                        if (members.Add(predecessor))
                            pending.Push(predecessor);
                    }
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Determines the forwarding blocks: blocks other than the entry and loop
    /// headers that emit nothing but an unconditional branch to another block.
    /// </summary>
    /// <remarks>
    /// The phi copies of the outgoing edge are emitted on every edge into a
    /// forwarding block, so a block with copies is only forwarding if it has
    /// a single predecessor.
    /// </remarks>
    private void ComputeForwardingBlocks()
    {
        foreach (var block in _method.Blocks)
        {
            if (block != _method.EntryBlock
                && !_loops.ContainsKey(block)
                && block.TerminationKind == BlockTerminationKind.Unconditional
                && block.Successors[0] != block
                && block.FirstPhiValue is null
                && _placement.GetPlacedBlock(block).Count < 1
                && (block.Predecessors.Length < 2
                    || !HasEdgeCopies(block, block.Successors[0])))
            {
                _forwarding.Add(block);
            }
        }
    }

    /// <summary>
    /// Returns true if the edge from source to target assigns a phi.
    /// </summary>
    private static bool HasEdgeCopies(BasicBlock source, BasicBlock target)
    {
        foreach (var phi in target.PhiValues)
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Sources[i] == source && phi.Arguments[i] != phi)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the immediate dominator of the given block that is not a
    /// forwarding block.
    /// </summary>
    private BasicBlock GetOwningDominator(BasicBlock block)
    {
        var dominator = _dominators.GetImmediateDominator(block);
        while (_forwarding.Contains(dominator))
            dominator = _dominators.GetImmediateDominator(dominator);
        return dominator;
    }

    /// <summary>
    /// Returns the block an edge to the given target leads to after passing
    /// through forwarding blocks, or null if it continues a loop.
    /// </summary>
    private BasicBlock? ResolveForwarding(BasicBlock source, BasicBlock target)
    {
        while (!IsBackEdge(source, target) && _forwarding.Contains(target))
        {
            source = target;
            target = target.Successors[0];
        }
        return IsBackEdge(source, target) ? null : target;
    }

    /// <summary>
    /// Determines the follow blocks and their owners.
    /// </summary>
    private void ComputeFollowBlocks()
    {
        // Count the forward edges of the tree into each block, looking
        // through forwarding blocks
        var forwardEdges = new Dictionary<BasicBlock, int>();
        foreach (var block in _method.Blocks)
        {
            if (_forwarding.Contains(block))
                continue;
            foreach (var successor in GetBranchTargets(block))
            {
                if (ResolveForwarding(block, successor) is not { } target)
                    continue;
                forwardEdges.TryGetValue(target, out int count);
                forwardEdges[target] = count + 1;
            }
        }

        foreach (var block in _method.Blocks)
        {
            if (block == _method.EntryBlock || _forwarding.Contains(block))
                continue;

            // A block reached by several forward edges is a merge block owned
            // by its immediate dominator
            var owner = GetOwningDominator(block);
            bool isFollow = forwardEdges.TryGetValue(block, out int count)
                && count > 1;

            // A block outside a loop that contains its immediate dominator is
            // a loop exit: it is placed after the outermost such loop
            HashSet<BasicBlock>? outermost = null;
            foreach (var (header, members) in _loops)
            {
                if (!members.Contains(owner) || members.Contains(block))
                    continue;
                if (outermost is null || members.Count > outermost.Count)
                {
                    outermost = members;
                    owner = header;
                }
            }

            if (!isFollow && outermost is null)
                continue;

            _followBlocks.Add(block);
            if (!_ownedFollowBlocks.TryGetValue(owner, out var owned))
            {
                owned = [];
                _ownedFollowBlocks.Add(owner, owned);
            }
            owned.Add(block);
        }

        // Follow blocks with the highest order are placed outermost, as their
        // code comes last
        foreach (var (_, owned) in _ownedFollowBlocks)
            owned.Sort((left, right) => _order[right].CompareTo(_order[left]));
    }

    #endregion

    #region Tree Construction

    /// <summary>
    /// Builds the structured statement tree of the method.
    /// </summary>
    public Node BuildTree() => BuildTree(_method.EntryBlock);

    /// <summary>
    /// Builds the statements of the given block, its loop (if it is a loop
    /// header) and all blocks it owns.
    /// </summary>
    private Node BuildTree(BasicBlock block)
    {
        if (!_ownedFollowBlocks.TryGetValue(block, out var owned))
            owned = [];

        if (!_loops.TryGetValue(block, out var members))
            return Nest(owned, 0, BuildBlock(block));

        // Follow blocks outside the loop are placed after the loop, follow
        // blocks inside the loop are part of its body
        var inside = new List<BasicBlock>();
        var outside = new List<BasicBlock>();
        foreach (var follow in owned)
            (members.Contains(follow) ? inside : outside).Add(follow);

        var header = BuildBlock(block);
        var loop = new LoopNode(block, members, Nest(inside, 0, header));
        if (inside.Count < 1)
            loop.HeaderTest = GetLoopTest(header, members, block, isLatch: false);
        loop.LatchTest = FindLatchTest(loop);
        return Nest(outside, 0, loop);
    }

    /// <summary>
    /// Returns the loop test of the given block statements if they end in a
    /// two-way branch with one side leaving the loop. For a latch, the other
    /// side must continue the loop directly.
    /// </summary>
    /// <remarks>
    /// Only the exact shape of <see cref="BuildBlock"/> for a conditional
    /// block is accepted: the block values followed by the branch, whose
    /// exit side is the phi copies of the exit edge (and of the edges through
    /// forwarding blocks) followed by the jump to the exit block. Anything
    /// else keeps the endless loop form.
    /// </remarks>
    private static LoopTest? GetLoopTest(
        SequenceNode statements,
        HashSet<BasicBlock> members,
        BasicBlock header,
        bool isLatch)
    {
        if (statements.Nodes.Count != 2 || statements.Nodes[1] is not IfNode branch)
            return null;

        // The exit jump may continue an outer loop: the edge chain then ends
        // in the forwarding latch of the outer loop
        static bool IsExit(Node node, HashSet<BasicBlock> members) =>
            IsEdgeChain(node, out var edge, out var tail)
            && tail is JumpNode jump
            && jump.Target.Block == edge.Target
            && !members.Contains(edge.Target);
        static bool IsContinue(Node node, BasicBlock header) =>
            IsEdgeChain(node, out _, out var tail)
            && tail is JumpNode jump
            && jump.Target == new JumpTarget(header, IsContinue: true);
        static bool StaysInLoop(Node node, HashSet<BasicBlock> members) =>
            IsEdgeChain(node, out _, out _)
            && members.Contains(((EdgeNode)((SequenceNode)node).Nodes[0]).Target);

        foreach (bool exitsOnTrue in new[] { true, false })
        {
            var exit = exitsOnTrue ? branch.TrueNode : branch.FalseNode;
            var stay = exitsOnTrue ? branch.FalseNode : branch.TrueNode;
            if (IsExit(exit, members) && (isLatch
                ? IsContinue(stay, header)
                : StaysInLoop(stay, members)))
            {
                return new LoopTest(branch, exitsOnTrue);
            }
        }
        return null;
    }

    /// <summary>
    /// Returns true if the given statement is a branch built by
    /// <see cref="BuildBranch"/>: a chain of edges followed by a jump or the
    /// inlined code of the last edge target.
    /// </summary>
    /// <param name="node">The statement.</param>
    /// <param name="last">The last edge of the chain.</param>
    /// <param name="tail">The statement after the chain.</param>
    internal static bool IsEdgeChain(Node node, out EdgeNode last, out Node tail)
    {
        last = null!;
        tail = null!;
        if (node is not SequenceNode { Nodes.Count: > 1 } sequence)
            return false;
        for (int i = 0; i + 1 < sequence.Nodes.Count; i++)
        {
            if (sequence.Nodes[i] is not EdgeNode edge)
                return false;
            if (i > 0 && edge.Source != last.Target)
                return false;
            last = edge;
        }
        tail = sequence.Nodes[^1];
        return true;
    }

    /// <summary>
    /// Finds the branch of the only back edge of a loop if it is the last
    /// statement on every path through the body that does not leave the body
    /// by a jump or return.
    /// </summary>
    private static LoopTest? FindLatchTest(LoopNode loop)
    {
        // The loop must have a single back edge
        BasicBlock? latch = null;
        int backEdges = 0;
        foreach (var member in loop.Members)
        {
            foreach (var successor in GetBranchTargets(member))
            {
                if (successor != loop.Header)
                    continue;
                latch = member;
                ++backEdges;
            }
        }
        if (backEdges != 1 || latch is null)
            return null;

        // Follow the tail of the body: the last statement of sequences and the
        // only side of branches whose other side never falls through
        var node = loop.Body;
        while (true)
        {
            switch (node)
            {
                case SequenceNode { Nodes.Count: > 0 } sequence:
                    if (sequence.Nodes[^1] is IfNode last && last.Block == latch)
                    {
                        return GetLoopTest(
                            sequence,
                            loop.Members,
                            loop.Header,
                            isLatch: true);
                    }
                    node = sequence.Nodes[^1];
                    break;
                case IfNode branch when AlwaysLeaves(branch.TrueNode):
                    node = branch.FalseNode;
                    break;
                case IfNode branch when AlwaysLeaves(branch.FalseNode):
                    node = branch.TrueNode;
                    break;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Returns true if every path through the given statement ends in a jump
    /// or a return, i.e. never reaches the statement after it in the tree.
    /// Unlike the emitter, this treats a jump to the next statement as a
    /// jump as well.
    /// </summary>
    private static bool AlwaysLeaves(Node node) =>
        node switch
        {
            SequenceNode sequence => sequence.Nodes.Count > 0
                && AlwaysLeaves(sequence.Nodes[^1]),
            BlockNode block =>
                block.Block.TerminationKind == BlockTerminationKind.Return,
            IfNode ifNode => AlwaysLeaves(ifNode.TrueNode)
                && AlwaysLeaves(ifNode.FalseNode),
            JumpNode => true,
            _ => false
        };

    /// <summary>
    /// Wraps the given node into labeled regions for the given follow blocks
    /// (outermost first), each followed by the code of its follow block.
    /// </summary>
    private Node Nest(List<BasicBlock> follows, int index, Node inner)
    {
        if (index >= follows.Count)
            return inner;
        var follow = follows[index];
        var labeled = new LabeledNode(follow, Nest(follows, index + 1, inner));
        return new SequenceNode([labeled, BuildTree(follow)]);
    }

    /// <summary>
    /// Builds the values and the terminator of a single block.
    /// </summary>
    private SequenceNode BuildBlock(BasicBlock block)
    {
        var nodes = new List<Node> { new BlockNode(block) };
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                nodes.Add(BuildBranch(block, block.Successors[0]));
                break;
            case BlockTerminationKind.Conditional:
                var conditional = block.AsConditionalView();
                nodes.Add(new IfNode(
                    block,
                    BuildBranch(block, conditional.TrueTarget),
                    BuildBranch(block, conditional.FalseTarget)));
                break;
            case BlockTerminationKind.Switch:
                nodes.Add(BuildSwitch(block));
                break;
        }
        return new SequenceNode(nodes);
    }

    /// <summary>
    /// Builds a switch whose cases are grouped by their targets.
    /// </summary>
    private SwitchNode BuildSwitch(BasicBlock block)
    {
        var switchView = block.AsSwitchView();
        var defaultTarget = switchView.DefaultTarget;
        var targets = new List<BasicBlock>();
        var values = new Dictionary<BasicBlock, List<int>>();
        for (int i = 0; i < switchView.NumCases; i++)
        {
            var target = switchView.GetCaseTarget(i);
            if (target == defaultTarget)
                continue;
            if (!values.TryGetValue(target, out var caseValues))
            {
                caseValues = [];
                values.Add(target, caseValues);
                targets.Add(target);
            }
            caseValues.Add(i);
        }

        var cases = new List<(List<int> Values, Node Body)>(targets.Count);
        foreach (var target in targets)
            cases.Add((values[target], BuildBranch(block, target)));
        return new SwitchNode(block, cases, BuildBranch(block, defaultTarget));
    }

    /// <summary>
    /// Builds the phi copies of an edge followed by the jump to its target,
    /// or by the inlined code of the target if this edge is its only entry.
    /// </summary>
    /// <remarks>
    /// An edge to a forwarding block is followed by the edge to its successor
    /// in the same sequence: the result is a chain of edges ending in the jump
    /// or the inlined code.
    /// </remarks>
    private SequenceNode BuildBranch(BasicBlock source, BasicBlock target)
    {
        var nodes = new List<Node> { new EdgeNode(source, target) };
        while (!IsBackEdge(source, target) && _forwarding.Contains(target))
        {
            source = target;
            target = target.Successors[0];
            nodes.Add(new EdgeNode(source, target));
        }

        if (IsBackEdge(source, target))
            nodes.Add(new JumpNode(new JumpTarget(target, IsContinue: true)));
        else if (_followBlocks.Contains(target))
            nodes.Add(new JumpNode(new JumpTarget(target, IsContinue: false)));
        else
            nodes.Add(BuildTree(target));
        return new SequenceNode(nodes);
    }

    #endregion
}

/// <summary>
/// The target of a jump: the start of the next iteration of the loop headed
/// by the block, or the code of a follow block.
/// </summary>
/// <param name="Block">The target block.</param>
/// <param name="IsContinue">True if the jump continues a loop.</param>
readonly record struct JumpTarget(BasicBlock Block, bool IsContinue);
