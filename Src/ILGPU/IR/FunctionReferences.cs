// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionReferences.cs
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

namespace ILGPU.IR
{
    /// <summary>
    /// Represents references to other top-level functions.
    /// </summary>
    public readonly struct FunctionReferences
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all references.
        /// </summary>
        public struct Enumerator : IEnumerator<TopLevelFunction>
        {
            #region Instance

            private HashSet<TopLevelFunction>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="references">The source references.</param>
            internal Enumerator(in FunctionReferences references)
            {
                enumerator = references.topLevelFunctions.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current function reference.
            /// </summary>
            public TopLevelFunction Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current" />
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.MoveNext" />
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset" />
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IDisposable.Dispose" />
            public void Dispose()
            {
                enumerator.Dispose();
            }

            #endregion
        }

        /// <summary>
        /// Represents a specific target and argument visitor.
        /// </summary>
        /// <typeparam name="TPredicate">The view predicate type.</typeparam>
        private readonly struct ReferencesVistor<TPredicate>
            : FunctionCall.ITargetVisitor
            , FunctionCall.IFunctionArgumentVisitor
            where TPredicate : IFunctionCollectionPredicate
        {
            private readonly TPredicate predicate;

            public ReferencesVistor(
                HashSet<TopLevelFunction> references,
                TPredicate currentPredicate)
            {
                References = references;
                predicate = currentPredicate;
            }

            /// <summary>
            /// Returns the associated references collection.
            /// </summary>
            public HashSet<TopLevelFunction> References { get; }

            /// <summary cref="FunctionCall.ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value callTarget)
            {
                if (callTarget is TopLevelFunction function &&
                    predicate.Match(function))
                    References.Add(function);
                return true;
            }

            /// <summary cref="FunctionCall.IFunctionArgumentVisitor.VisitFunctionArgument(FunctionValue)"/>
            public void VisitFunctionArgument(FunctionValue functionValue)
            {
                if (functionValue is TopLevelFunction function &&
                    predicate.Match(function))
                    References.Add(function);
            }
        }

        #endregion

        private readonly HashSet<TopLevelFunction> topLevelFunctions;

        /// <summary>
        /// Computes function references to all called functions.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="scope">The source scope.</param>
        /// <param name="predicate">The current predicate.</param>
        /// <returns>A references instance.</returns>
        public static FunctionReferences Create<TPredicate>(
            Scope scope,
            TPredicate predicate)
            where TPredicate : IFunctionCollectionPredicate
        {
            Debug.Assert(scope != null, "Invalid scope");

            var references = new HashSet<TopLevelFunction>();
            var referencesVisitor = new ReferencesVistor<TPredicate>(references, predicate);
            scope.VisitCallTargetsAndFunctionArguments(
                ref referencesVisitor,
                ref referencesVisitor);

            return new FunctionReferences(
                scope.Entry,
                references);
        }

        /// <summary>
        /// Constructs a function references instance.
        /// </summary>
        /// <param name="sourceFunction">The source function.</param>
        /// <param name="references">All function references.</param>
        private FunctionReferences(
            FunctionValue sourceFunction,
            HashSet<TopLevelFunction> references)
        {
            SourceFunction = sourceFunction;
            topLevelFunctions = references;
        }

        /// <summary>
        /// Returns the associated source function.
        /// </summary>
        public FunctionValue SourceFunction { get; }

        /// <summary>
        /// Returns the number of function references.
        /// </summary>
        public int Count => topLevelFunctions.Count;

        /// <summary>
        /// Returns true if the number of function references is zero.
        /// </summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// Tries to resolve the first reference.
        /// </summary>
        /// <param name="firstReference">The first resolved reference.</param>
        /// <param name="enumerator">The resolved enumerator.</param>
        /// <returns>True, if the first reference could be resolved.</returns>
        [MethodImpl]
        public bool TryGetFirstReference(
            out TopLevelFunction firstReference,
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
        /// Returns an enumerator to enumerate all function references.
        /// </summary>
        /// <returns>An enumerator to enumerate all function references.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);
    }
}
