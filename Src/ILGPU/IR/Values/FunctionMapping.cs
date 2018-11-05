// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionMapping.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an object that can be managed in the scope of a <see cref="FunctionMapping{T}"/>
    /// </summary>
    public interface IFunctionMappingObject
    {
        /// <summary>
        /// Returns the associated function handle.
        /// </summary>
        FunctionHandle Handle { get; }

        /// <summary>
        /// Returns the original source method (may be null).
        /// </summary>
        MethodBase Source { get; }
    }

    /// <summary>
    /// Maps function handles and managed .Net methods to <see cref="TopLevelFunction"/>
    /// objects.
    /// </summary>
    /// <typeparam name="T">The mapped type.</typeparam>
    public sealed class FunctionMapping<T>
        where T : class, IFunctionMappingObject
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            #region Instance

            private Dictionary<FunctionHandle, T>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="mapping">The handle mapping.</param>
            internal Enumerator(Dictionary<FunctionHandle, T> mapping)
            {
                enumerator = mapping.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public T Current => enumerator.Current.Value;

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
            public void Dispose() => enumerator.Dispose();

            #endregion
        }

        /// <summary>
        /// Represents a readonly view.
        /// </summary>
        public readonly struct ReadOnlyCollection : IEnumerable<T>
        {
            #region Instance

            internal ReadOnlyCollection(FunctionMapping<T> parent)
            {
                Parent = parent;
            }

            #endregion

            #region Properties

            internal FunctionMapping<T> Parent { get; }

            /// <summary>
            /// Returns the number of stored functions.
            /// </summary>
            public int Count => Parent.Count;

            /// <summary>
            /// Returns data that corresponds to the given handle.
            /// </summary>
            /// <param name="handle">The function handle.</param>
            /// <returns>The resolved data.</returns>
            public T this[FunctionHandle handle] => Parent[handle];

            #endregion

            #region Methods

            /// <summary>
            /// Tries to resolve the given managed method to function reference.
            /// </summary>
            /// <param name="method">The method to resolve.</param>
            /// <param name="handle">The resolved function handle (if any).</param>
            /// <returns>True, iff the requested function could be resolved.</returns>
            public bool TryGetHandle(MethodBase method, out FunctionHandle handle) =>
                Parent.TryGetHandle(method, out handle);

            /// <summary>
            /// Tries to resolve the given handle to a top-level function.
            /// </summary>
            /// <param name="handle">The function handle to resolve.</param>
            /// <param name="data">The resolved data (if any).</param>
            /// <returns>True, iff the requested function could be resolved.</returns>
            public bool TryGetFunction(FunctionHandle handle, out T data) =>
                Parent.TryGetData(handle, out data);

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns an enumerator that enumerates all stored values.
            /// </summary>
            /// <returns>An enumerator that enumerates all stored values.</returns>
            public Enumerator GetEnumerator() => Parent.GetEnumerator();

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        #endregion

        #region Instance

        private readonly Dictionary<MethodBase, FunctionHandle> managedMethods =
            new Dictionary<MethodBase, FunctionHandle>();
        private readonly Dictionary<FunctionHandle, T> topLevelFunctions =
            new Dictionary<FunctionHandle, T>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of stored functions.
        /// </summary>
        public int Count => topLevelFunctions.Count;

        /// <summary>
        /// Returns data that corresponds to the given handle.
        /// </summary>
        /// <param name="handle">The function handle.</param>
        /// <returns>The resolved data.</returns>
        public T this[FunctionHandle handle]
        {
            get
            {
                if (handle.IsEmpty)
                    return default;
                return topLevelFunctions[handle];
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Constructs a readonly object view.
        /// </summary>
        /// <returns>The created readonly object view.</returns>
        public ReadOnlyCollection AsReadOnly() => new ReadOnlyCollection(this);

        /// <summary>
        /// Tries to resolve the given managed method to function reference.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="handle">The resolved function handle (if any).</param>
        /// <returns>True, iff the requested function could be resolved.</returns>
        public bool TryGetHandle(MethodBase method, out FunctionHandle handle) =>
            managedMethods.TryGetValue(method, out handle);

        /// <summary>
        /// Tries to resolve the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="data">The resolved data (if any).</param>
        /// <returns>True, iff the requested function could be resolved.</returns>
        public bool TryGetData(FunctionHandle method, out T data) =>
            topLevelFunctions.TryGetValue(method, out data);

        /// <summary>
        /// Registers the handle with the given data object.
        /// </summary>
        /// <param name="handle">The function handle.</param>
        /// <param name="data">The data object to register.</param>
        public void Register(FunctionHandle handle, T data)
        {
            if (handle.IsEmpty)
                throw new ArgumentNullException(nameof(handle));
            topLevelFunctions[handle] = data ?? throw new ArgumentNullException(nameof(data));
            var source = data.Source;
            if (source != null)
                managedMethods[source] = handle;
        }

        /// <summary>
        /// Removes the given function handle.
        /// </summary>
        /// <param name="handle">The function handle.</param>
        /// <returns>True, if this handle could be removed.</returns>
        public bool Remove(FunctionHandle handle)
        {
            if (!TryGetData(handle, out T data))
                return false;
            topLevelFunctions.Remove(handle);
            var source = data.Source;
            if (source != null)
                managedMethods.Remove(source);
            return true;
        }

        /// <summary>
        /// Converts this mapping object into an array.
        /// </summary>
        /// <returns>The array.</returns>
        public T[] ToArray()
        {
            var result = new T[Count];
            topLevelFunctions.Values.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// Converts this mapping object into an immutable array.
        /// </summary>
        /// <returns>The immutable array.</returns>
        public ImmutableArray<T> ToImmutableArray()
        {
            var builder = ImmutableArray.CreateBuilder<T>(Count);
            foreach (var value in this)
                builder.Add(value);
            return builder.MoveToImmutable();
        }

        /// <summary>
        /// Clears all contained functions.
        /// </summary>
        public void Clear()
        {
            managedMethods.Clear();
            topLevelFunctions.Clear();
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(topLevelFunctions);

        #endregion
    }
}
