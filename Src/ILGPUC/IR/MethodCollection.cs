// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Represents a function predicate for functions to transform.
        /// </summary>
        public readonly struct ToTransform : IMethodCollectionPredicate
        {
            /// <summary>
            /// Constructs a new function predicate.
            /// </summary>
            /// <param name="flags">The desired flags that should not be set.</param>
            public ToTransform(MethodTransformationFlags flags)
            {
                Flags = flags;
            }

            /// <summary>
            /// Returns the flags that should not be set on the target function.
            /// </summary>
            public MethodTransformationFlags Flags { get; }

            /// <summary cref="IMethodCollectionPredicate.Match(Method)"/>
            public bool Match(Method method) =>
                method.HasImplementation &&
                (method.TransformationFlags & Flags) == MethodTransformationFlags.None;
        }

        /// <summary>
        /// Represents a predicate based on a hash set implementation.
        /// </summary>
        public readonly struct SetPredicate : IMethodCollectionPredicate
        {
            private readonly HashSet<Method> methodSet;

            /// <summary>
            /// Constructs a new set predicate using a method collection.
            /// </summary>
            /// <param name="methods">The method collection to use.</param>
            public SetPredicate(in MethodCollection methods)
                : this(methods.ToSet())
            { }

            /// <summary>
            /// Constructs a new set predicate using a method set.
            /// </summary>
            /// <param name="methods">The method set to use.</param>
            public SetPredicate(HashSet<Method> methods)
            {
                methodSet = methods;
            }

            /// <summary>
            /// Returns true if the given method is contained in the encapsulated set.
            /// </summary>
            public readonly bool Match(Method method) => methodSet.Contains(method);
        }
    }

    /// <summary>
    /// Represents a thread-safe function view.
    /// </summary>
    public readonly struct MethodCollection : IEnumerable<Method>
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
            void IDisposable.Dispose() { }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new method collection.
        /// </summary>
        /// <param name="context">The parent context.</param>
        /// <param name="collection">The collection members.</param>
        internal MethodCollection(IRContext context, ImmutableArray<Method> collection)
        {
            Context = context;
            Collection = collection;
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
        /// Returns the number of functions.
        /// </summary>
        public readonly int Count => Collection.Length;

        #endregion

        #region Methods

        /// <summary>
        /// Converts this collection into a <see cref="HashSet{T}"/> instance.
        /// </summary>
        /// <returns>The created and filled set instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly HashSet<Method> ToSet()
        {
            var result = new HashSet<Method>();
            foreach (var method in this)
                result.Add(method);
            return result;
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(Collection);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Method> IEnumerable<Method>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
