// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ControlFlowDirection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses.ControlFlowDirection
{
    /// <summary>
    /// Defines an abstract control flow-analysis source that has an entry block and
    /// the ability to find a unique exit block.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public interface IControlFlowAnalysisSource<TDirection>
        where TDirection : IControlFlowDirection
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
    public interface IControlFlowDirection
    {
        /// <summary>
        /// Returns true if this is a forwards direction.
        /// </summary>
        bool IsForwards { get; }

        /// <summary>
        /// Returns the entry block for a given source.
        /// </summary>
        /// <typeparam name="TSource">The source base.</typeparam>
        /// <typeparam name="TDirection">The current direction.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The entry block.</returns>
        BasicBlock GetEntryBlock<TSource, TDirection>(in TSource source)
            where TSource : IControlFlowAnalysisSource<TDirection>
            where TDirection : struct, IControlFlowDirection;
    }

    /// <summary>
    /// Defines the default forward control-flow direction.
    /// </summary>
    public readonly struct Forwards : IControlFlowDirection
    {
        /// <summary>
        /// Returns true.
        /// </summary>
        public readonly bool IsForwards => true;

        /// <summary>
        /// Returns the entry in case of a forwards source, the exit block otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BasicBlock GetEntryBlock<TSource, TDirection>(
            in TSource source)
            where TSource : IControlFlowAnalysisSource<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            TDirection direction = default;
            return direction.IsForwards ? source.EntryBlock : source.FindExitBlock();
        }
    }

    /// <summary>
    /// Defines the backwards control-flow direction in which predecessors are considered
    /// to be successors and vice versa.
    /// </summary>
    public readonly struct Backwards : IControlFlowDirection
    {
        /// <summary>
        /// Returns false.
        /// </summary>
        public readonly bool IsForwards => false;

        /// <summary>
        /// Returns the entry in case of a backwards source, the exit block otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BasicBlock GetEntryBlock<TSource, TDirection>(
            in TSource source)
            where TSource : IControlFlowAnalysisSource<TDirection>
            where TDirection : struct, IControlFlowDirection
        {
            TDirection direction = default;
            return direction.IsForwards ? source.FindExitBlock() : source.EntryBlock;
        }
    }
}
