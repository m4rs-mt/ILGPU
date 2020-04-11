// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MethodCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
    public interface IMethodCollectionPredicate
    {
        /// <summary>
        /// Returns true if this predicate matches the given function.
        /// </summary>
        /// <param name="method">The function to test.</param>
        /// <returns>True, if this predicate matches the given function.</returns>
        bool Match(Method method);
    }

    /// <summary>
    /// Represents an abstract function view.
    /// </summary>
    public interface IMethodCollection : IReadOnlyCollection<Method>
    {
        /// <summary>
        /// Returns the associated IR context.
        /// </summary>
        IRContext Context { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        int TotalNumMethods { get; }
    }

    /// <summary>
    /// Represents an abstract function view using a predicate.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public interface IMethodCollection<TPredicate> : IMethodCollection
        where TPredicate : IMethodCollectionPredicate
    {
        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        TPredicate Predicate { get; }
    }

    /// <summary>
    /// Represents useful extensions for function views.
    /// </summary>
    public static class MethodCollections
    {
        /// <summary>
        /// Represents a function predicate that matches all functions.
        /// </summary>
        public readonly struct AllMethods : IMethodCollectionPredicate
        {
            /// <summary cref="IMethodCollectionPredicate.Match(Method)"/>
            public bool Match(Method method) => true;
        }

        /// <summary>
        /// Represents a function predicate that matches all functions that have not
        /// been transformed yet.
        /// </summary>
        public readonly struct NotTransformed : IMethodCollectionPredicate
        {
            /// <summary cref="IMethodCollectionPredicate.Match(Method)"/>
            public bool Match(Method method) =>
                !method.HasTransformationFlags(MethodTransformationFlags.Transformed);
        }

        /// <summary>
        /// Represents a function predicate that matches all dirty functions.
        /// </summary>
        public readonly struct Dirty : IMethodCollectionPredicate
        {
            /// <summary cref="IMethodCollectionPredicate.Match(Method)"/>
            public bool Match(Method method) =>
                !method.HasTransformationFlags(MethodTransformationFlags.Dirty);
        }
    }

    /// <summary>
    /// Represents an unsafe function view.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public readonly struct UnsafeMethodCollection<TPredicate> :
        IMethodCollection<TPredicate>
        where TPredicate : IMethodCollectionPredicate
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<Method>
        {
            #region Instance

            private MethodMapping<Method>.Enumerator enumerator;
            private readonly TPredicate predicate;

            /// <summary>
            /// Constructs a new internal enumerator.
            /// </summary>
            /// <param name="collection">The parent collection.</param>
            /// <param name="currentPredicate">The view predicate.</param>
            internal Enumerator(
                MethodMapping<Method>.ReadOnlyCollection collection,
                TPredicate currentPredicate)
            {
                enumerator = collection.GetEnumerator();
                predicate = currentPredicate;
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public Method Current => enumerator.Current;

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

        internal UnsafeMethodCollection(
            IRContext context,
            MethodMapping<Method>.ReadOnlyCollection collection,
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
        public MethodMapping<Method>.ReadOnlyCollection Collection { get; }

        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        public TPredicate Predicate { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        public int TotalNumMethods => Collection.Count;

        /// <summary cref="IReadOnlyCollection{T}.Count"/>
        int IReadOnlyCollection<Method>.Count => TotalNumMethods;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Collection, Predicate);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Method> IEnumerable<Method>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Represents a thread-safe function view.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public readonly struct MethodCollection<TPredicate> :
        IMethodCollection<TPredicate>
        where TPredicate : IMethodCollectionPredicate
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<Method>
        {
            #region Instance

            private ImmutableArray<Method>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new internal enumerator.
            /// </summary>
            /// <param name="collection">The parent collection.</param>
            internal Enumerator(ImmutableArray<Method> collection)
            {
                enumerator = collection.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public Method Current => enumerator.Current;

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

        internal MethodCollection(
            IRContext context,
            ImmutableArray<Method> collection,
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
        public ImmutableArray<Method> Collection { get; }

        /// <summary>
        /// Returns the associated predicate.
        /// </summary>
        public TPredicate Predicate { get; }

        /// <summary>
        /// Returns the total number of functions without applying the predicate.
        /// </summary>
        public int TotalNumMethods => Collection.Length;

        /// <summary cref="IReadOnlyCollection{T}.Count"/>
        int IReadOnlyCollection<Method>.Count => TotalNumMethods;

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Collection);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Method> IEnumerable<Method>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
