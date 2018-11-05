// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a predicate on a function.
    /// </summary>
    public interface IFunctionCollectionPredicate
    {
        /// <summary>
        /// Returns true if this predicate matches the given function.
        /// </summary>
        /// <param name="topLevelFunction">The function to test.</param>
        /// <returns>True, if this predicate matches the given function.</returns>
        bool Match(TopLevelFunction topLevelFunction);
    }

    /// <summary>
    /// Represents an abstract function view.
    /// </summary>
    public interface IFunctionCollection : IReadOnlyCollection<TopLevelFunction>
    {
        /// <summary>
        /// Returns the associated IR context.
        /// </summary>
        IRContext Context { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        int TotalNumFunctions { get; }
    }

    /// <summary>
    /// Represents an abstract function view using a predicate.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public interface IFunctionCollection<TPredicate> : IFunctionCollection
        where TPredicate : IFunctionCollectionPredicate
    {
        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        TPredicate Predicate { get; }
    }

    /// <summary>
    /// Represents useful extensions for function views.
    /// </summary>
    public static class FunctionCollections
    {
        /// <summary>
        /// Represents a function predicate that matches all functions.
        /// </summary>
        public readonly struct AllFunctions : IFunctionCollectionPredicate
        {
            /// <summary cref="IFunctionCollectionPredicate.Match(TopLevelFunction)"/>
            public bool Match(TopLevelFunction topLevelFunction) => true;
        }

        /// <summary>
        /// Represents a function predicate that matches all functions that have not been transformed yet.
        /// </summary>
        public readonly struct NotTransformed : IFunctionCollectionPredicate
        {
            /// <summary cref="IFunctionCollectionPredicate.Match(TopLevelFunction)"/>
            public bool Match(TopLevelFunction topLevelFunction) =>
                !topLevelFunction.HasTransformationFlags(TopLevelFunctionTransformationFlags.Transformed);
        }

        /// <summary>
        /// Represents a function predicate that matches all dirty functions.
        /// </summary>
        public readonly struct Dirty : IFunctionCollectionPredicate
        {
            /// <summary cref="IFunctionCollectionPredicate.Match(TopLevelFunction)"/>
            public bool Match(TopLevelFunction topLevelFunction) =>
                !topLevelFunction.HasTransformationFlags(TopLevelFunctionTransformationFlags.Dirty);
        }

        /// <summary>
        /// Creates a new IR builder.
        /// </summary>
        /// <param name="functionView">The parent function view.</param>
        /// <returns>The created IR builder.</returns>
        public static IRBuilder CreateBuilder<TFunctionView>(this TFunctionView functionView)
            where TFunctionView : IFunctionCollection =>
            functionView.Context.CreateBuilder();

        /// <summary>
        /// Creates a new IR builder.
        /// </summary>
        /// <param name="functionView">The parent function view.</param>
        /// <param name="flags">The builder flags.</param>
        /// <returns>The created IR builder.</returns>
        public static IRBuilder CreateBuilder<TFunctionView>(this TFunctionView functionView, IRBuilderFlags flags)
            where TFunctionView : IFunctionCollection =>
            functionView.Context.CreateBuilder(flags);
    }

    /// <summary>
    /// Represents an unsafe function view.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public readonly struct UnsafeFunctionCollection<TPredicate> : IFunctionCollection<TPredicate>
        where TPredicate : IFunctionCollectionPredicate
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<TopLevelFunction>
        {
            #region Instance

            private FunctionMapping<TopLevelFunction>.Enumerator enumerator;
            private readonly TPredicate predicate;

            /// <summary>
            /// Constructs a new internal enumerator.
            /// </summary>
            /// <param name="collection">The parent collection.</param>
            /// <param name="currentPredicate">The view predicate.</param>
            internal Enumerator(
                FunctionMapping<TopLevelFunction>.ReadOnlyCollection collection,
                TPredicate currentPredicate)
            {
                enumerator = collection.GetEnumerator();
                predicate = currentPredicate;
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public TopLevelFunction Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                while (enumerator.MoveNext())
                {
                    if (predicate.Match(enumerator.Current))
                        return true;
                }
                return false;
            }

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => enumerator.Dispose();

            #endregion
        }

        #endregion

        #region Instance

        internal UnsafeFunctionCollection(
            IRContext context,
            FunctionMapping<TopLevelFunction>.ReadOnlyCollection collection,
            TPredicate predicate)
        {
            Debug.Assert(context != null, "Invalid context");
            Context = context;
            Collection = collection;
            Predicate = predicate;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated IR context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the associated function collection.
        /// </summary>
        public FunctionMapping<TopLevelFunction>.ReadOnlyCollection Collection { get; }

        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        public TPredicate Predicate { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        public int TotalNumFunctions => Collection.Count;

        /// <summary cref="IReadOnlyCollection{T}.Count"/>
        int IReadOnlyCollection<TopLevelFunction>.Count => TotalNumFunctions;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Collection, Predicate);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<TopLevelFunction> IEnumerable<TopLevelFunction>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Represents a thread-safe function view.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public readonly struct FunctionCollection<TPredicate> : IFunctionCollection<TPredicate>
        where TPredicate : IFunctionCollectionPredicate
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<TopLevelFunction>
        {
            #region Instance

            private ImmutableArray<TopLevelFunction>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new internal enumerator.
            /// </summary>
            /// <param name="collection">The parent collection.</param>
            internal Enumerator(ImmutableArray<TopLevelFunction> collection)
            {
                enumerator = collection.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public TopLevelFunction Current => enumerator.Current;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            #endregion
        }

        #endregion

        #region Instance

        internal FunctionCollection(
            IRContext context,
            ImmutableArray<TopLevelFunction> collection,
            TPredicate predicate)
        {
            Debug.Assert(context != null, "Invalid context");
            Context = context;
            Collection = collection;
            Predicate = predicate;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated IR context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the associated function collection.
        /// </summary>
        public ImmutableArray<TopLevelFunction> Collection { get; }

        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        public TPredicate Predicate { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        public int TotalNumFunctions => Collection.Length;

        /// <summary cref="IReadOnlyCollection{T}.Count"/>
        int IReadOnlyCollection<TopLevelFunction>.Count => TotalNumFunctions;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Collection);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<TopLevelFunction> IEnumerable<TopLevelFunction>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
