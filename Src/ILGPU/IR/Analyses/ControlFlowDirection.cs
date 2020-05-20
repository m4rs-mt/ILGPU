// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ControlFlowDirection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses.ControlFlowDirection
{
    /// <summary>
    /// Defines a control flow direction.
    /// </summary>
    public interface IControlFlowDirection
    {
        /// <summary>
        /// Determines the actual predecessor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of predecessors.</returns>
        TCollection GetPredecessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T>;

        /// <summary>
        /// Determines the actual successor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of successor.</returns>
        TCollection GetSuccessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T>;
    }

    /// <summary>
    /// Defines the default forward control flow direction.
    /// </summary>
    public readonly struct Forwards : IControlFlowDirection
    {
        /// <summary>
        /// Determines the actual predecessor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of predecessors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCollection GetPredecessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T> =>
            predecessors;

        /// <summary>
        /// Determines the actual successor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of successor.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCollection GetSuccessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T> =>
            successors;
    }

    /// <summary>
    /// Defines the backwards control flow direction in which predecessors are considered
    /// to be successors and vice versa.
    /// </summary>
    public readonly struct Backwards : IControlFlowDirection
    {
        /// <summary>
        /// Determines the actual predecessor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of predecessors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCollection GetPredecessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T> =>
            successors;

        /// <summary>
        /// Determines the actual successor collection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="predecessors">The list of predecessors (forwards).</param>
        /// <param name="successors">The list of successors (forwards).</param>
        /// <returns>The collection of successor.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCollection GetSuccessors<T, TCollection>(
            TCollection predecessors,
            TCollection successors)
            where TCollection : IReadOnlyCollection<T> =>
            predecessors;
    }
}
