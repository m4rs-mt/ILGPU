// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: TraversalOrder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Describes an abstract traversal set that tracks unique values.
/// </summary>
/// <typeparam name="T">The value type</typeparam>
interface ITraversalSet<T> { bool Add(T value); }

/// <summary>
/// A enumeration state of a generic traversal.
/// </summary>
/// <param name="Index">The current enumeration index.</param>
record struct TraversalEnumerationState(int Index);

/// <summary>
/// Provides successors for a given basic block.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
interface ITraversalSuccessorsProvider<T, TDirection>
    where T : Value
    where TDirection : struct, IControlFlowDirection
{
    /// <summary>
    /// Returns or computes successors of the given value.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <returns>The returned successor collection.</returns>
    static abstract ReadOnlySpan<T> GetSuccessors(T value);
}

/// <summary>
/// A general traversal visitor.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
interface ITraversalVisitor<T>
{
    /// <summary>
    /// Visits the given block.
    /// </summary>
    /// <param name="value">The value to visit.</param>
    void Visit(T value);
}

/// <summary>
/// A generic collection visitor.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <typeparam name="TCollection">The collection type.</typeparam>
/// <param name="collection">The target collection.</param>
readonly struct TraversalCollectionVisitor<T, TCollection>(TCollection collection) :
    ITraversalVisitor<T>
    where TCollection : ICollection<T>
{
    /// <summary>
    /// Returns the target collection to add the elements to.
    /// </summary>
    public TCollection Collection { get; } = collection;

    /// <summary>
    /// Adds the given block to the target collection.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void Visit(T value) => Collection.Add(value);
}

/// <summary>
/// A generic traversal order.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
interface ITraversalOrder<T> where T : Value
{
    /// <summary>
    /// Initializes a new enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    static abstract TraversalEnumerationState Init<TCollection>(TCollection values)
        where TCollection : IReadOnlyList<T>;

    /// <summary>
    /// Tries to move the state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    static abstract bool MoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T>;

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry value.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The created traversal.</returns>
    static abstract void Traverse<TSet, TVisitor, TSuccessorProvider, TDirection>(
        T entry,
        TSet set,
        ref TVisitor visitor)
        where TSet : ITraversalSet<T>
        where TVisitor : struct, ITraversalVisitor<T>, allows ref struct
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection;
}

/// <summary>
/// Another view that is compatible with the current type without requiring a new
/// computation.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <typeparam name="TOther">The other view.</typeparam>
interface ICompatibleTraversalOrder<T, TOther>
    where T : Value
    where TOther : struct, ITraversalOrder<T>;

/// <summary>
/// A helper class for traversal.
/// </summary>
/// <typeparam name="T">The value type</typeparam>
static class TraversalOrder<T> where T : Value
{
    /// <summary>
    /// Specifies the default initial stack size.
    /// </summary>
    public const int InitStackSize = 16;

    /// <summary>
    /// Initializes a forwards enumeration state.
    /// </summary>
    public static TraversalEnumerationState ForwardsInit() => new() { Index = -1 };

    /// <summary>
    /// Tries to move a forwards state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool ForwardsMoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T> =>
        ++state.Index < values.Count;

    /// <summary>
    /// Initializes a backwards enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    public static TraversalEnumerationState BackwardsInit<TCollection>(
        TCollection values)
        where TCollection : IReadOnlyList<T> =>
        new() { Index = values.Count };

    /// <summary>
    /// Tries to move a backwards state to the next block.
    /// </summary>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool BackwardsMoveNext(ref TraversalEnumerationState state) =>
        --state.Index >= 0;

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TOrder">The current order type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry value.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>The created traversal.</returns>
    public static ImmutableArray<T>.Builder
        TraverseToCollectionBuilder<TSet, TOrder, TSuccessorProvider, TDirection>(
        T entry,
        TSet set,
        int count = 8)
        where TSet : ITraversalSet<T>
        where TOrder : struct, ITraversalOrder<T>
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        var newValues = ImmutableArray.CreateBuilder<T>(count);
        var visitor = new TraversalCollectionVisitor<
            T,
            ImmutableArray<T>.Builder>(newValues);

        TOrder.Traverse<
            TSet,
            TraversalCollectionVisitor<T, ImmutableArray<T>.Builder>,
            TSuccessorProvider,
            TDirection>(entry, set, ref visitor);

        return newValues;
    }
}

/// <summary>
/// A helper class for traversal.
/// </summary>
static class TraversalOrder
{
    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TOrder">The current order type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entryBlock">The entry block.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>The created traversal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BasicBlockCollection<TOrder, TDirection>
        TraverseToCollection<TOrder, TSuccessorProvider, TDirection>(
        this BasicBlock entryBlock,
        int count = 8)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TSuccessorProvider :
            struct, ITraversalSuccessorsProvider<BasicBlock, TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        var builder = TraversalOrder<BasicBlock>.TraverseToCollectionBuilder<
            ValueSet<Method, BasicBlock>,
            TOrder,
            TSuccessorProvider,
            TDirection>(
                entryBlock,
                entryBlock.Method.CreateSet<BasicBlock>(),
                count);
        return new BasicBlockCollection<TOrder, TDirection>(
            entryBlock,
            builder.ToImmutable());
    }
}

/// <summary>
/// Enumerates all basic values in pre order.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
readonly struct PreOrder<T> :
    ITraversalOrder<T>,
    ICompatibleTraversalOrder<T, ReversePreOrder<T>>
    where T : Value
{
    /// <summary>
    /// Initializes a new enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    public static TraversalEnumerationState Init<TCollection>(TCollection values)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.ForwardsInit();

    /// <summary>
    /// Tries to move the state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool MoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.ForwardsMoveNext(values, ref state);

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The created traversal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Traverse<
        TSet,
        TVisitor,
        TSuccessorProvider,
        TDirection>(
        T entry,
        TSet set,
        ref TVisitor visitor)
        where TSet : ITraversalSet<T>
        where TVisitor : struct, ITraversalVisitor<T>, allows ref struct
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        var stack = new Stack<T>(TraversalOrder<T>.InitStackSize);
        var current = entry;

        while (true)
        {
            if (set.Add(current))
            {
                visitor.Visit(current);
                var successors = TSuccessorProvider.GetSuccessors(current);
                if (successors.Length > 0)
                {
                    for (int i = successors.Length - 1; i >= 1; --i)
                        stack.Push(successors[i]);
                    current = successors[0];
                    continue;
                }
            }

            if (stack.Count < 1)
                break;
            current = stack.Pop();
        }
    }
}

/// <summary>
/// Enumerates all basic blocks in reverse pre order.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
readonly struct ReversePreOrder<T> :
    ITraversalOrder<T>,
    ICompatibleTraversalOrder<T, PreOrder<T>>
    where T : Value
{
    /// <summary>
    /// Initializes a new enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    public static TraversalEnumerationState Init<TCollection>(TCollection values)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.BackwardsInit(values);

    /// <summary>
    /// Tries to move the state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool MoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.BackwardsMoveNext(ref state);

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The created traversal.</returns>
    public static void Traverse<
        TSet,
        TVisitor,
        TSuccessorProvider,
        TDirection>(
        T entry,
        TSet set,
        ref TVisitor visitor)
        where TSet : ITraversalSet<T>
        where TVisitor : struct, ITraversalVisitor<T>, allows ref struct
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection =>
        PreOrder<T>.Traverse<TSet, TVisitor, TSuccessorProvider, TDirection>(
            entry,
            set,
            ref visitor);
}

/// <summary>
/// Enumerates all basic blocks in post order.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
readonly struct PostOrder<T> :
    ITraversalOrder<T>,
    ICompatibleTraversalOrder<T, ReversePostOrder<T>>
    where T : Value
{
    /// <summary>
    /// Initializes a new enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    public static TraversalEnumerationState Init<TCollection>(TCollection values)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.ForwardsInit();

    /// <summary>
    /// Tries to move the state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool MoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.ForwardsMoveNext(values, ref state);

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry value.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The created traversal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Traverse<
        TSet,
        TVisitor,
        TSuccessorProvider,
        TDirection>(
        T entry,
        TSet set,
        ref TVisitor visitor)
        where TSet : ITraversalSet<T>
        where TVisitor : struct, ITraversalVisitor<T>, allows ref struct
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        var stack = new Stack<(T, int)>(TraversalOrder<T>.InitStackSize);
        var current = (Value: entry, Child: 0);

        while (true)
        {
            var currentValue = current.Value;

            if (current.Child == 0)
            {
                if (!set.Add(currentValue))
                    goto next;
            }

            var successors = TSuccessorProvider.GetSuccessors(currentValue);
            if (current.Child >= successors.Length)
            {
                visitor.Visit(currentValue);
                goto next;
            }
            else
            {
                stack.Push((currentValue, current.Child + 1));
                current = (successors[current.Child], 0);
            }

            continue;
        next:
            if (stack.Count < 1)
                break;
            current = stack.Pop();
        }
    }
}

/// <summary>
/// Enumerates all basic values in reverse post order.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
readonly struct ReversePostOrder<T> :
    ITraversalOrder<T>,
    ICompatibleTraversalOrder<T, PostOrder<T>>
    where T : Value
{
    /// <summary>
    /// Initializes a new enumeration state.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    public static TraversalEnumerationState Init<TCollection>(TCollection values)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.BackwardsInit(values);

    /// <summary>
    /// Tries to move the state to the next block.
    /// </summary>
    /// <param name="values">The list of values to enumerate.</param>
    /// <param name="state">The current enumeration state.</param>
    /// <returns>True, if there is a next block.</returns>
    public static bool MoveNext<TCollection>(
        TCollection values,
        ref TraversalEnumerationState state)
        where TCollection : IReadOnlyList<T> =>
        TraversalOrder<T>.BackwardsMoveNext(ref state);

    /// <summary>
    /// Computes a traversal using the current order.
    /// </summary>
    /// <typeparam name="TSet">The current traversal set type.</typeparam>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <typeparam name="TSuccessorProvider">The successor provider.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="entry">The entry value.</param>
    /// <param name="set">A set compatible with the current traversal.</param>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The created traversal.</returns>
    public static void Traverse<
        TSet,
        TVisitor,
        TSuccessorProvider,
        TDirection>(
        T entry,
        TSet set,
        ref TVisitor visitor)
        where TSet : ITraversalSet<T>
        where TVisitor : struct, ITraversalVisitor<T>, allows ref struct
        where TSuccessorProvider : struct, ITraversalSuccessorsProvider<T, TDirection>
        where TDirection : struct, IControlFlowDirection =>
        PostOrder<T>.Traverse<TSet, TVisitor, TSuccessorProvider, TDirection>(
            entry,
            set,
            ref visitor);
}
