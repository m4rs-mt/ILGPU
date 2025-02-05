// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: References.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Analyses.ControlFlowDirection;
using ILGPUC.IR.Analyses.TraversalOrders;
using ILGPUC.IR.Values;
using System;
using System.Collections.Generic;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents references to other methods.
/// </summary>
readonly struct References
{
    #region Static

    /// <summary>
    /// Computes all direct method references to all called methods.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="predicate">The current predicate.</param>
    /// <returns>A references instance.</returns>
    public static References Create(Method method, Predicate<Method> predicate) =>
        Create(method.Blocks, predicate);

    /// <summary>
    /// Computes all direct method references to all called methods.
    /// </summary>
    /// <typeparam name="TOrder">The order collection.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="collection">The block collection.</param>
    /// <param name="predicate">The current predicate.</param>
    /// <returns>A references instance.</returns>
    public static References Create<TOrder, TDirection>(
        in BasicBlockCollection<TOrder, TDirection> collection,
        Predicate<Method>? predicate = null)
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        var references = new HashSet<Method>();
        var referencesList = new List<Method>();
        collection.ForEachValue<MethodCall>(call =>
        {
            var target = call.Target;
            if (predicate is not null && !predicate(target))
                return;

            if (references.Add(target))
                referencesList.Add(target);
        });
        return new References(
            collection.Method,
            references,
            referencesList);
    }

    /// <summary>
    /// Computes all direct and indirect method references to all called methods.
    /// </summary>
    /// <param name="collection">The block collection.</param>
    /// <param name="predicate">The current predicate.</param>
    /// <returns>A references instance.</returns>
    public static References CreateRecursive(
        BasicBlockCollection<ReversePostOrder, Forwards> collection,
        Predicate<Method>? predicate = null)
    {
        var references = new HashSet<Method>();
        var referencesList = new List<Method>();
        var method = collection.Method;
        var toProcess = new Stack<Method>();

        references.Add(method);
        referencesList.Add(method);

        for (; ; )
        {
            collection.ForEachValue<MethodCall>(call =>
            {
                var target = call.Target;
                if (predicate is not null && !predicate(target))
                    return;

                if (references.Add(target))
                {
                    referencesList.Add(target);
                    toProcess.Push(target);
                }
            });

            if (toProcess.Count < 1)
                break;
            collection = toProcess.Pop().Blocks;
        }

        return new References(
            method,
            references,
            referencesList);
    }

    #endregion

    #region Instance

    private readonly HashSet<Method> _methodSet;
    private readonly List<Method> _methodList;

    /// <summary>
    /// Constructs a references instance.
    /// </summary>
    /// <param name="method">The source method.</param>
    /// <param name="referenceSet">The set of all method references.</param>
    /// <param name="referenceList">The list of all method references.</param>
    private References(
        Method method,
        HashSet<Method> referenceSet,
        List<Method> referenceList)
    {
        SourceMethod = method;
        _methodSet = referenceSet;
        _methodList = referenceList;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated source function.
    /// </summary>
    public Method SourceMethod { get; }

    /// <summary>
    /// Returns the number of function references.
    /// </summary>
    public int Count => _methodList.Count;

    /// <summary>
    /// Returns true if the number of function references is zero.
    /// </summary>
    public bool IsEmpty => Count < 1;

    #endregion

    #region Methods

    /// <summary>
    /// Returns true if the given method is referenced.
    /// </summary>
    /// <param name="method">The method to test.</param>
    /// <returns>True, if the given method is referenced.</returns>
    public bool HasReferenceTo(Method method) => _methodSet.Contains(method);

    /// <summary>
    /// Returns an enumerator to enumerate all method references.
    /// </summary>
    /// <returns>An enumerator to enumerate all method references.</returns>
    public List<Method>.Enumerator GetEnumerator() => _methodList.GetEnumerator();

    #endregion
}
