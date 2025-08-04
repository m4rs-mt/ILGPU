// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPUC.IR;

/// <summary>
/// Represents a thread-safe function view.
/// </summary>
/// <param name="Context">The parent context.</param>
/// <param name="Collection">The collection members.</param>
readonly record struct MethodCollection(
    IRContext Context,
    ImmutableArray<Method> Collection) : IEnumerable<Method>
{
    /// <summary>
    /// Returns the number of functions.
    /// </summary>
    public int Count => Collection.Length;

    /// <summary>
    /// Converts this collection into a <see cref="HashSet{T}"/> instance.
    /// </summary>
    /// <returns>The created and filled set instance.</returns>
    public HashSet<Method> ToSet()
    {
        var result = new HashSet<Method>();
        foreach (var method in this)
            result.Add(method);
        return result;
    }

    /// <summary>
    /// Returns an enumerator that enumerates all stored values.
    /// </summary>
    /// <returns>An enumerator that enumerates all stored values.</returns>
    public readonly ImmutableArray<Method>.Enumerator GetEnumerator() =>
        Collection.GetEnumerator();

    /// <summary cref="IEnumerable{T}.GetEnumerator"/>
    IEnumerator<Method> IEnumerable<Method>.GetEnumerator()
    {
        foreach (var method in this)
            yield return method;
    }

    /// <summary cref="IEnumerable.GetEnumerator"/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var method in this)
            yield return method;
    }
}
