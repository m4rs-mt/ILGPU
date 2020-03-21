// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: References.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
                enumerator = references.methods.GetEnumerator();
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
        /// Computes method references to all called methods.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="scope">The source scope.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public static References Create<TPredicate>(
            Scope scope,
            TPredicate predicate)
            where TPredicate : IMethodCollectionPredicate
        {
            Debug.Assert(scope != null, "Invalid scope");

            var references = new HashSet<Method>();
            var referencesList = new List<Method>();
            scope.ForEachValue<MethodCall>(call =>
            {
                var target = call.Target;
                if (!predicate.Match(target))
                    return;

                if (references.Add(target))
                    referencesList.Add(target);
            });
            return new References(scope, referencesList);
        }

        #endregion

        #region Instance

        private readonly List<Method> methods;

        /// <summary>
        /// Constructs a references instance.
        /// </summary>
        /// <param name="scope">The source scope.</param>
        /// <param name="references">All method references.</param>
        private References(Scope scope, List<Method> references)
        {
            Scope = scope;
            methods = references;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated source function.
        /// </summary>
        public Method SourceMethod => Scope.Method;

        /// <summary>
        /// Returns the parent scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the number of function references.
        /// </summary>
        public int Count => methods.Count;

        /// <summary>
        /// Returns true if the number of function references is zero.
        /// </summary>
        public bool IsEmpty => Count == 0;

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the first reference.
        /// </summary>
        /// <param name="firstReference">The first resolved reference.</param>
        /// <param name="enumerator">The resolved enumerator.</param>
        /// <returns>True, if the first reference could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFirstReference(
            out Method firstReference,
            out Enumerator enumerator)
        {
            enumerator = GetEnumerator();
            if (IsEmpty)
            {
                firstReference = null;
                return false;
            }
            else
            {
                enumerator.MoveNext();
                firstReference = enumerator.Current;
                return true;
            }
        }

        /// <summary>
        /// Returns an enumerator to enumerate all method references.
        /// </summary>
        /// <returns>An enumerator to enumerate all method references.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        #endregion
    }

    /// <summary>
    /// Represents a collection of all references.
    /// </summary>
    public readonly struct AllReferences
    {
        #region Static

        /// <summary>
        /// Computes method references to all methods recursively.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TScopeProvider">The provider to resolve methods to scopes.</typeparam>
        /// <param name="sourceMethod">The source method.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <param name="scopeProvider">Resolves methods to scopes.</param>
        /// <returns>A references instance.</returns>
        public static AllReferences Create<TPredicate, TScopeProvider>(
            Method sourceMethod,
            in TPredicate predicate,
            TScopeProvider scopeProvider)
            where TPredicate : IMethodCollectionPredicate
            where TScopeProvider : IScopeProvider =>
            Create(scopeProvider[sourceMethod], predicate, scopeProvider);

        /// <summary>
        /// Computes method references to all methods recursively.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TScopeProvider">The provider to resolve methods to scopes.</typeparam>
        /// <param name="sourceScope">The source scope.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <param name="scopeProvider">Resolves methods to scopes.</param>
        /// <returns>A references instance.</returns>
        public static AllReferences Create<TPredicate, TScopeProvider>(
            Scope sourceScope,
            in TPredicate predicate,
            TScopeProvider scopeProvider)
            where TPredicate : IMethodCollectionPredicate
            where TScopeProvider : IScopeProvider
        {
            Debug.Assert(sourceScope != null, "Invalid scope");

            var mapping = new Dictionary<Method, References>();

            var mainReferences = sourceScope.ComputeReferences(predicate);
            mapping.Add(mainReferences.SourceMethod, mainReferences);

            if (mainReferences.TryGetFirstReference(
                out Method current,
                out References.Enumerator mainEnumerator))
            {
                var toProcess = new Stack<Method>();
                for (; ; )
                {
                    if (!mapping.ContainsKey(current))
                    {
                        var scope = scopeProvider[current];
                        var references = scope.ComputeReferences(predicate);
                        mapping.Add(current, references);

                        foreach (var reference in references)
                            toProcess.Push(reference);
                    }
                    if (toProcess.Count < 1)
                    {
                        if (mainEnumerator.MoveNext())
                            current = mainEnumerator.Current;
                        else
                            break;
                    }
                    else
                        current = toProcess.Pop();
                }
            }
            mainEnumerator.Dispose();

            return new AllReferences(mapping);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new references instance.
        /// </summary>
        /// <param name="mapping">The underyling mapping.</param>
        private AllReferences(Dictionary<Method, References> mapping)
        {
            Debug.Assert(mapping != null, "Invalid mapping");
            Mapping = mapping;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Stores the internal mapping dictionary.
        /// </summary>
        private Dictionary<Method, References> Mapping { get; }

        /// <summary>
        /// Resolves method references for the given method.
        /// </summary>
        /// <param name="method">The source method.</param>
        /// <returns>The resolved references instance.</returns>
        public References this[Method method] => Mapping[method];

        #endregion

        #region Methods

        /// <summary>
        /// Returns an enumerator to enumerate all method references.
        /// </summary>
        /// <returns>An enumerator to enumerate all method references.</returns>
        public Dictionary<Method, References>.Enumerator GetEnumerator() => Mapping.GetEnumerator();

        #endregion
    }
}
