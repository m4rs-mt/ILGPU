// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ControlFlowDirection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// Defines an abstract control flow-analysis source that has an entry block and
/// the ability to find a unique exit block.
/// </summary>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
interface IControlFlowAnalysisSource<TDirection>
    where TDirection : struct, IControlFlowDirection
{
    /// <summary>
    /// Returns the entry block.
    /// </summary>
    BasicBlock EntryBlock { get; }

    /// <summary>
    /// Computes the exit block.
    /// </summary>
    /// <returns>The exit block.</returns>
    BasicBlock FindExitBlock();
}

/// <summary>
/// Defines a control-flow direction.
/// </summary>
interface IControlFlowDirection
{
    /// <summary>
    /// Returns true if this is a forwards direction.
    /// </summary>
    static abstract bool IsForwards { get; }

    /// <summary>
    /// Returns the entry block for a given source.
    /// </summary>
    /// <typeparam name="TSource">The source base.</typeparam>
    /// <typeparam name="TDirection">The current direction.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The entry block.</returns>
    static abstract BasicBlock GetEntryBlock<TSource, TDirection>(in TSource source)
        where TSource : struct, IControlFlowAnalysisSource<TDirection>
        where TDirection : struct, IControlFlowDirection;
}

/// <summary>
/// Defines the default forward control-flow direction.
/// </summary>
readonly struct Forwards : IControlFlowDirection
{
    /// <summary>
    /// Returns true.
    /// </summary>
    public static bool IsForwards => true;

    /// <summary>
    /// Returns the entry in case of a forwards source, the exit block otherwise.
    /// </summary>
    public static BasicBlock GetEntryBlock<TSource, TDirection>(in TSource source)
        where TSource : struct, IControlFlowAnalysisSource<TDirection>
        where TDirection : struct, IControlFlowDirection =>
        TDirection.IsForwards ? source.EntryBlock : source.FindExitBlock();
}

/// <summary>
/// Defines the backwards control-flow direction in which predecessors are considered
/// to be successors and vice versa.
/// </summary>
readonly struct Backwards : IControlFlowDirection
{
    /// <summary>
    /// Returns false.
    /// </summary>
    public static bool IsForwards => false;

    /// <summary>
    /// Returns the entry in case of a backwards source, the exit block otherwise.
    /// </summary>
    public static BasicBlock GetEntryBlock<TSource, TDirection>(
        in TSource source)
        where TSource : struct, IControlFlowAnalysisSource<TDirection>
        where TDirection : struct, IControlFlowDirection =>
        TDirection.IsForwards ? source.FindExitBlock() : source.EntryBlock;
}
