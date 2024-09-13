// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: References.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents references to other methods.
    /// </summary>
    public readonly struct References
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all references.
        /// </summary>
        public struct Enumerator : IEnumerator<Method>
        {
            #region Instance

            private List<Method>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="references">The source references.</param>
            internal Enumerator(in References references)
            {
                enumerator = references.methodList.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current function reference.
            /// </summary>
            public Method Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose() { }

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Computes all direct method references to all called methods.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="method">The method.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public static References Create<TPredicate>(
            Method method,
            in TPredicate predicate)
            where TPredicate : struct, IMethodCollectionPredicate =>
            Create(method.Blocks, predicate);

        /// <summary>
        /// Computes all direct method references to all called methods.
        /// </summary>
        /// <typeparam name="TOrder">The order collection.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="collection">The block collection.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public static References Create<TOrder, TDirection, TPredicate>(
            in BasicBlockCollection<TOrder, TDirection> collection,
            TPredicate predicate)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
            where TPredicate : struct, IMethodCollectionPredicate
        {
            var references = new HashSet<Method>();
            var referencesList = new List<Method>();
            collection.ForEachValue<MethodCall>(call =>
            {
                var target = call.Target;
                if (!predicate.Match(target))
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
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="collection">The block collection.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public static References CreateRecursive<TPredicate>(
            BasicBlockCollection<ReversePostOrder, Forwards> collection,
            TPredicate predicate)
            where TPredicate : struct, IMethodCollectionPredicate
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
                    if (!predicate.Match(target))
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

        private readonly HashSet<Method> methodSet;
        private readonly List<Method> methodList;

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
            methodSet = referenceSet;
            methodList = referenceList;
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
        public readonly int Count => methodList.Count;

        /// <summary>
        /// Returns true if the number of function references is zero.
        /// </summary>
        public readonly bool IsEmpty => Count < 1;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given method is referenced.
        /// </summary>
        /// <param name="method">The method to test.</param>
        /// <returns>True, if the given method is referenced.</returns>
        public readonly bool HasReferenceTo(Method method) =>
            methodSet.Contains(method);

        /// <summary>
        /// Returns an enumerator to enumerate all method references.
        /// </summary>
        /// <returns>An enumerator to enumerate all method references.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(this);

        #endregion
    }
}
