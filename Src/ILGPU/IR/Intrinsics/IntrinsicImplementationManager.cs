// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: IntrinsicImplementationManager.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Intrinsics
{
    /// <summary>
    /// Represents an abstract intrinsic manager that caches intrinsic methods.
    /// </summary>
    public interface IIntrinsicImplementationManager
    {
        /// <summary>
        /// Creates a specialized and typed intrinsic provider for the given backend.
        /// </summary>
        /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
        /// <param name="backend">The backend.</param>
        /// <returns>The created implementation provider.</returns>
        IntrinsicImplementationProvider<TDelegate> CreateProvider<TDelegate>(Backend backend)
            where TDelegate : Delegate;
    }

    /// <summary>
    /// Represents an intrinisc manager that caches intrinsic methods.
    /// </summary>
    public sealed partial class IntrinsicImplementationManager : IIntrinsicImplementationManager
    {
        #region Nested Types

        /// <summary>
        /// Represents a single entry that is associated with a matcher.
        /// It stores several possible intrinsic implementations for specific backends.
        /// </summary>
        internal sealed class ImplementationEntry : IIntrinsicImplementation, IEnumerable<IntrinsicImplementation>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to enumerate all implementations in the scope of an entry.
            /// </summary>
            public struct Enumerator : IEnumerator<IntrinsicImplementation>
            {
                private HashSet<IntrinsicImplementation>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new implementation enumerator.
                /// </summary>
                /// <param name="implementationSet">The implementations.</param>
                internal Enumerator(HashSet<IntrinsicImplementation> implementationSet)
                {
                    enumerator = implementationSet.GetEnumerator();
                }

                /// <summary>
                /// Returns the current implementation.
                /// </summary>
                public IntrinsicImplementation Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() => enumerator.Dispose();

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private readonly HashSet<IntrinsicImplementation> implementations = new HashSet<IntrinsicImplementation>();

            #endregion

            #region Methods

            /// <summary>
            /// Registers the given implementation with the current entry.
            /// </summary>
            /// <param name="implementation">The implementation to register.</param>
            public void Register(IntrinsicImplementation implementation)
            {
                implementations.Add(implementation ?? throw new ArgumentNullException(nameof(implementation)));
            }

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns a new enumerator to iterate over all implementations.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(implementations);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<IntrinsicImplementation> IEnumerable<IntrinsicImplementation>.GetEnumerator() =>
                GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Represents a mapping of matchers to backend-type specific implementations.
        /// </summary>
        internal struct BackendContainer
        {
            #region Static

            /// <summary>
            /// Creates a new backend container.
            /// </summary>
            public static BackendContainer Create() =>
                new BackendContainer()
                {
                    matchers = IntrinsicMatcher.CreateMatchers<ImplementationEntry>()
                };

            #endregion

            #region Instance

            private IntrinsicMatcher<ImplementationEntry>[] matchers;

            #endregion

            #region Properties

            /// <summary>
            /// Returns teh associated intrinsic matcher.
            /// </summary>
            /// <param name="kind">The matcher kind.</param>
            /// <returns>The resolved intrinsic matcher.</returns>
            public IntrinsicMatcher<ImplementationEntry> this[IntrinsicMatcher.MatcherKind kind] =>
                matchers[(int)kind];

            #endregion

            #region Methods

            /// <summary>
            /// Transforms all internal entries using the transformation provided.
            /// </summary>
            /// <typeparam name="TOther">The other matcher type.</typeparam>
            /// <typeparam name="TTransformer">The transformer type to use.</typeparam>
            /// <param name="transformer">The transformer instance.</param>
            /// <param name="otherMatchers">The other matchers (target array).</param>
            public void TransformTo<TOther, TTransformer>(
                TTransformer transformer,
                IntrinsicMatcher<TOther>[] otherMatchers)
                where TOther : class, IIntrinsicImplementation
                where TTransformer : struct, IIntrinsicImplementationTransformer<ImplementationEntry, TOther>
            {
                if (otherMatchers == null)
                    throw new ArgumentNullException(nameof(otherMatchers));
                if (otherMatchers.Length < matchers.Length)
                    throw new ArgumentOutOfRangeException(nameof(otherMatchers));

                for (int i = 0, e = matchers.Length; i < e; ++i)
                    matchers[i].TransformTo(transformer, otherMatchers[i]);
            }
        }

        #endregion

        #endregion

        #region Instance

        /// <summary>
        /// Stores all intrinsic containers.
        /// </summary>
        private readonly BackendContainer[] containers;

        /// <summary>
        /// Constructs a new empty implementation manager.
        /// </summary>
        public IntrinsicImplementationManager()
        {
            var backendType = Enum.GetValues(typeof(BackendType));
            containers = new BackendContainer[backendType.Length];
            for (int i = 0, e = containers.Length; i < e; ++i)
                containers[i] = BackendContainer.Create();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Resolves the associated intrinsic container for the given backend type.
        /// </summary>
        /// <param name="backendType">The backend type.</param>
        /// <returns>The resolved intrinsic container.</returns>
        private BackendContainer this[BackendType backendType] => containers[(int)backendType];

        #endregion

        #region Methods

        /// <summary>
        /// Resolves an intrinsic matcher.
        /// </summary>
        /// <typeparam name="TMatcher">The matcher type.</typeparam>
        /// <param name="kind">The matcher kind.</param>
        /// <param name="implementation">The implementation to use.</param>
        /// <returns>The resolved matcher.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TMatcher ResolveMatcher<TMatcher>(
            IntrinsicMatcher.MatcherKind kind,
            IntrinsicImplementation implementation)
            where TMatcher : IntrinsicMatcher<ImplementationEntry>
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));
            var container = this[implementation.BackendType];
            return container[kind] as TMatcher;
        }

        /// <summary>
        /// Registers the given intrinsic implementation.
        /// </summary>
        /// <param name="method">The method information.</param>
        /// <param name="implementation">The intrinsic implementation.</param>
        public void RegisterMethod(MethodInfo method, IntrinsicImplementation implementation)
        {
            var matcher = ResolveMatcher<IntrinsicMethodMatcher<ImplementationEntry>>(
                IntrinsicMatcher.MatcherKind.Method,
                implementation);
            if (!matcher.TryGetImplementation(method, out var entry))
            {
                entry = new ImplementationEntry();
                matcher.Register(method, entry);
            }
            entry.Register(implementation);
        }

        /// <summary>
        /// Creates a specialized and typed intrinsic provider for the given backend.
        /// </summary>
        /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
        /// <param name="backend">The backend.</param>
        /// <returns>The created implementation provider.</returns>
        public IntrinsicImplementationProvider<TDelegate> CreateProvider<TDelegate>(Backend backend)
            where TDelegate : Delegate
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));
            return new IntrinsicImplementationProvider<TDelegate>(
                this[backend.BackendType],
                backend);
        }

        #endregion
    }
}
