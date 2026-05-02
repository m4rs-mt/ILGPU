// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TerminationViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// An abstract termination view.
/// </summary>
interface ITerminationView
{
    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    void CopyTo<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct;
}

/// <summary>
/// A view onto a basic block with unconditional termination (single successor).
/// </summary>
/// <param name="Block">The underlying basic block.</param>
readonly struct UnconditionalView(BasicBlock Block) : ITerminationView
{
    /// <summary>
    /// Returns the target block.
    /// </summary>
    public BasicBlock Target => Block.Successors[0];

    /// <summary>
    /// Returns the source location for this termination.
    /// </summary>
    public Location Location => Block.Location;

    /// <summary>
    /// Tries to create an unconditional view from a basic block.
    /// </summary>
    /// <param name="block">The block to convert.</param>
    /// <param name="view">The resulting view if successful.</param>
    /// <returns>True if the block has unconditional termination.</returns>
    public static bool TryCreate(
        BasicBlock block,
        out UnconditionalView view)
    {
        if (block.TerminationKind == BlockTerminationKind.Unconditional)
        {
            view = new UnconditionalView(block);
            return true;
        }
        view = default;
        return false;
    }

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    public void CopyTo<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct
    {
        var newTarget = rewriter.Rewrite(Target) as BasicBlock;
        rewriter.Builder.CreateUnconditionalTermination(newTarget);
    }
}

/// <summary>
/// A view onto a basic block with conditional termination (if-branch).
/// </summary>
/// <param name="Block">The underlying basic block.</param>
readonly struct ConditionalView(BasicBlock Block) : ITerminationView
{
    /// <summary>
    /// Returns the branch condition.
    /// </summary>
    public Value Condition => Block.TerminationCondition!;

    /// <summary>
    /// Returns the true branch target.
    /// </summary>
    public BasicBlock TrueTarget => Block.Successors[0];

    /// <summary>
    /// Returns the false branch target.
    /// </summary>
    public BasicBlock FalseTarget => Block.Successors[1];

    /// <summary>
    /// Returns the source location for this termination.
    /// </summary>
    public Location Location => Block.Location;

    /// <summary>
    /// Tries to create a conditional view from a basic block.
    /// </summary>
    /// <param name="block">The block to convert.</param>
    /// <param name="view">The resulting view if successful.</param>
    /// <returns>True if the block has conditional termination.</returns>
    public static bool TryCreate(BasicBlock block, out ConditionalView view)
    {
        if (block.TerminationKind == BlockTerminationKind.Conditional)
        {
            view = new ConditionalView(block);
            return true;
        }
        view = default;
        return false;
    }

    /// <summary>
    /// Returns true if the given block is one of the branch targets and outputs the other.
    /// </summary>
    /// <param name="block">The branch target to check.</param>
    /// <param name="otherBlock">The other branch target (if found).</param>
    public bool TryGetOtherTarget(
        BasicBlock block,
        [NotNullWhen(true)] out BasicBlock? otherBlock)
    {
        if (TrueTarget == block)
        {
            otherBlock = FalseTarget;
            return true;
        }
        if (FalseTarget == block)
        {
            otherBlock = TrueTarget;
            return true;
        }
        otherBlock = null;
        return false;
    }

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    public void CopyTo<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct
    {
        var newTrueTarget = rewriter.Rewrite(TrueTarget) as BasicBlock;
        var newFalseTarget = rewriter.Rewrite(FalseTarget) as BasicBlock;
        rewriter.Builder.CreateConditionalTermination(
            Condition,
            newTrueTarget,
            newFalseTarget);
    }
}

/// <summary>
/// A view onto a basic block with switch termination.
/// </summary>
/// <param name="Block">The underlying basic block.</param>
readonly struct SwitchView(BasicBlock Block) : ITerminationView
{
    /// <summary>
    /// A collection of all cases.
    /// </summary>
    internal readonly struct CasesCollection(SwitchView switchView)
    {
        /// <summary>
        /// Returns the default case target.
        /// </summary>
        public BasicBlock DefaultTarget => switchView.DefaultTarget;

        /// <summary>
        /// Returns the number of cases (excluding the default case).
        /// </summary>
        public int Count => switchView.NumCases;

        /// <summary>
        /// Returns an enumerator to iterate over all cases.
        /// </summary>
        public CaseEnumerator GetEnumerator() => new(switchView);
    }

    /// <summary>
    /// Enumerator for switch case targets.
    /// </summary>
    internal ref struct CaseEnumerator(SwitchView view)
    {
        private int _index = -1;

        /// <summary>
        /// Returns the current case target.
        /// </summary>
        public readonly BasicBlock Current => view.GetCaseTarget(_index);

        /// <summary>
        /// Moves to the next case.
        /// </summary>
        public bool MoveNext() => ++_index < view.NumCases;
    }

    /// <summary>
    /// Returns the switch condition.
    /// </summary>
    public Value Condition => Block.TerminationCondition!;

    /// <summary>
    /// Returns the default case target.
    /// </summary>
    public BasicBlock DefaultTarget => Block.Successors[0];

    /// <summary>
    /// Returns the number of switch cases (excluding default).
    /// </summary>
    public int NumCases => Block.Successors.Length - 1;

    /// <summary>
    /// Returns the source location for this termination.
    /// </summary>
    public Location Location => Block.Location;

    /// <summary>
    /// Returns a case collection to enumerate all cases.
    /// </summary>
    public CasesCollection Cases => new(this);

    /// <summary>
    /// Gets the target block for the specified case index.
    /// </summary>
    /// <param name="caseIndex">The 0-based case index.</param>
    /// <returns>The target block for that case.</returns>
    public BasicBlock GetCaseTarget(int caseIndex)
    {
        Block.Assert(caseIndex >= 0 && caseIndex < NumCases);
        return Block.Successors[caseIndex + 1];
    }

    /// <summary>
    /// Tries to create a switch view from a basic block.
    /// </summary>
    /// <param name="block">The block to convert.</param>
    /// <param name="view">The resulting view if successful.</param>
    /// <returns>True if the block has switch termination.</returns>
    public static bool TryCreate(
        BasicBlock block,
        out SwitchView view)
    {
        if (block.TerminationKind == BlockTerminationKind.Switch)
        {
            view = new SwitchView(block);
            return true;
        }
        view = default;
        return false;
    }

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    public void CopyTo<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct
    {
        var newDefaultTarget = rewriter.RewriteAs<BasicBlock>(DefaultTarget);
        var switchBuilder = rewriter.Builder.CreateSwitchTermination(
            Condition,
            capacity: NumCases + 1);
        switchBuilder.AddDefault(newDefaultTarget);

        foreach (var @case in Cases)
            switchBuilder.AddCase(rewriter.RewriteAs<BasicBlock>(@case));

        switchBuilder.Seal();
    }
}

/// <summary>
/// A view onto a basic block with return termination.
/// </summary>
/// <param name="Block">The underlying basic block.</param>
readonly struct ReturnView(BasicBlock Block) : ITerminationView
{
    /// <summary>
    /// Returns the return value.
    /// </summary>
    public Value ReturnValue => Block.TerminationValue.AsNotNull();

    /// <summary>
    /// Returns true if this is a void return.
    /// </summary>
    public bool IsVoidReturn => ReturnValue is UndefinedValue;

    /// <summary>
    /// Returns the source location for this termination.
    /// </summary>
    public Location Location => Block.Location;

    /// <summary>
    /// Tries to create a return view from a basic block.
    /// </summary>
    /// <param name="block">The block to convert.</param>
    /// <param name="view">The resulting view if successful.</param>
    /// <returns>True if the block has return termination.</returns>
    public static bool TryCreate(BasicBlock block, out ReturnView view)
    {
        if (block.TerminationKind == BlockTerminationKind.Return)
        {
            view = new ReturnView(block);
            return true;
        }
        view = default;
        return false;
    }

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    public void CopyTo<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct =>
        rewriter.Builder.CreateReturnTermination(ReturnValue);

    /// <summary>
    /// Copies the current return view to the given rewriter.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type to use.</typeparam>
    /// <param name="rewriter">The rewriter instance.</param>
    /// <param name="returnBlock">The return block to use for returns.</param>
    public void CopyTo<TRewriter>(in TRewriter rewriter, BasicBlock? returnBlock)
        where TRewriter : IBasicBlockRewriter, allows ref struct
    {
        if (returnBlock is null)
            CopyTo(rewriter);
        else
            rewriter.Builder.CreateUnconditionalTermination(returnBlock);
    }
}

/// <summary>
/// Extension methods for BasicBlock to support termination views.
/// </summary>
static class TerminationViewExtensions
{
    /// <summary>
    /// Tries to get an unconditional view of this block.
    /// </summary>
    public static bool TryGetUnconditionalView(
        this BasicBlock block,
        out UnconditionalView view) =>
        UnconditionalView.TryCreate(block, out view);

    /// <summary>
    /// Tries to get a conditional view of this block.
    /// </summary>
    public static bool TryGetConditionalView(
        this BasicBlock block,
        out ConditionalView view) =>
        ConditionalView.TryCreate(block, out view);

    /// <summary>
    /// Tries to get a switch view of this block.
    /// </summary>
    public static bool TryGetSwitchView(
        this BasicBlock block,
        out SwitchView view) =>
        SwitchView.TryCreate(block, out view);

    /// <summary>
    /// Tries to get a return view of this block.
    /// </summary>
    public static bool TryGetReturnView(
        this BasicBlock block,
        out ReturnView view) =>
        ReturnView.TryCreate(block, out view);

    /// <summary>
    /// Gets an unconditional view of this block.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the block doesn't have unconditional termination.
    /// </exception>
    public static UnconditionalView AsUnconditionalView(this BasicBlock block)
    {
        if (!TryGetUnconditionalView(block, out var view))
        {
            throw block.GetInvalidOperationException(
                $"Block {block} does not have unconditional termination");
        }
        return view;
    }

    /// <summary>
    /// Gets a conditional view of this block.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the block doesn't have conditional termination.
    /// </exception>
    public static ConditionalView AsConditionalView(this BasicBlock block)
    {
        if (!TryGetConditionalView(block, out var view))
        {
            throw block.GetInvalidOperationException(
                $"Block {block} does not have conditional termination");
        }
        return view;
    }

    /// <summary>
    /// Gets a switch view of this block.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the block doesn't have switch termination.
    /// </exception>
    public static SwitchView AsSwitchView(this BasicBlock block)
    {
        if (!TryGetSwitchView(block, out var view))
        {
            throw block.GetInvalidOperationException(
                $"Block {block} does not have switch termination");
        }
        return view;
    }

    /// <summary>
    /// Gets a return view of this block.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the block doesn't have return termination.
    /// </exception>
    public static ReturnView AsReturnView(this BasicBlock block)
    {
        if (!TryGetReturnView(block, out var view))
        {
            throw block.GetInvalidOperationException(
                $"Block {block} does not have return termination");
        }
        return view;
    }

    /// <summary>
    /// Dumps termination information of the given block to the text writer provided.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <param name="textWriter">The target text writer.</param>
    public static void DumpTermination(this BasicBlock block, TextWriter textWriter)
    {
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Pending:
                textWriter.Write($"pending {block.ToSuccessorsString()}");
                break;
            case BlockTerminationKind.Unconditional:
                textWriter.Write($"br {block.ToSuccessorsString()}");
                break;
            case BlockTerminationKind.Conditional:
                var condString = block.AsConditionalView().Condition.ToReferenceString();
                textWriter.Write($"cond.br {condString}{block.ToSuccessorsString()}");
                break;
            case BlockTerminationKind.Switch:
                textWriter.Write($"switch {block.ToSuccessorsString()}");
                break;
            case BlockTerminationKind.Return:
                if (block.AsReturnView().IsVoidReturn)
                    textWriter.Write("return");
                else
                {
                    var retString = block.TerminationValue
                        .AsNotNull().ToReferenceString();
                    textWriter.Write($"return {retString}");
                }
                break;
            default:
                throw new UnreachableException();
        }
        textWriter.WriteLine();
    }
}
