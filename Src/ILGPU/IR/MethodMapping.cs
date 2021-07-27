// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MethodMapping.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an object that can be managed in the scope of a
    /// <see cref="MethodMapping{T}"/>
    /// </summary>
    public interface IMethodMappingObject
    {
        /// <summary>
        /// Returns the associated function handle.
        /// </summary>
        MethodHandle Handle { get; }

        /// <summary>
        /// Returns the original source method (may be null).
        /// </summary>
        MethodBase Source { get; }
    }

    /// <summary>
    /// Maps function handles and managed .Net methods to <see cref="Method"/>
    /// objects.
    /// </summary>
    /// <typeparam name="T">The mapped type.</typeparam>
    public sealed class MethodMapping<T>
        where T : class, IMethodMappingObject
    {
        #region Nested Types

        /// <summary>
        /// The internal enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            #region Instance

            private List<T>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="dataList">The data list.</param>
            internal Enumerator(List<T> dataList)
            {
                enumerator = dataList.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary cref="IEnumerator{T}.Current"/>
            public T Current => enumerator.Current;

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

            internal ReadOnlyCollection(MethodMapping<T> parent)
            {
                Parent = parent;
            }

            #endregion

            #region Properties

            internal MethodMapping<T> Parent { get; }

            /// <summary>
            /// Returns the number of stored functions.
            /// </summary>
            public int Count => Parent.Count;

            /// <summary>
            /// Returns data that corresponds to the given handle.
            /// </summary>
            /// <param name="handle">The function handle.</param>
            /// <returns>The resolved data.</returns>
            public T this[MethodHandle handle] => Parent[handle];

            #endregion

            #region Methods

            /// <summary>
            /// Tries to resolve the given managed method to function reference.
            /// </summary>
            /// <param name="method">The method to resolve.</param>
            /// <param name="handle">The resolved function handle (if any).</param>
            /// <returns>True, if the requested function could be resolved.</returns>
            public bool TryGetHandle(MethodBase method, out MethodHandle handle) =>
                Parent.TryGetHandle(method, out handle);

            /// <summary>
            /// Tries to resolve the given handle to a top-level function.
            /// </summary>
            /// <param name="handle">The function handle to resolve.</param>
            /// <param name="data">The resolved data (if any).</param>
            /// <returns>True, if the requested function could be resolved.</returns>
            public bool TryGetFunction(MethodHandle handle, out T data) =>
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

        private readonly Dictionary<MethodBase, MethodHandle> managedMethods =
            new Dictionary<MethodBase, MethodHandle>();

        private readonly Dictionary<MethodHandle, int> methods =
            new Dictionary<MethodHandle, int>();

        private readonly List<T> dataList = new List<T>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of stored functions.
        /// </summary>
        public int Count => methods.Count;

        /// <summary>
        /// Returns data that corresponds to the given handle.
        /// </summary>
        /// <param name="handle">The function handle.</param>
        /// <returns>The resolved data.</returns>
        public T this[MethodHandle handle]
        {
            get
            {
                if (handle.IsEmpty)
                    return default;
                var index = methods[handle];
                return dataList[index];
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
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetHandle(MethodBase method, out MethodHandle handle) =>
            managedMethods.TryGetValue(method, out handle);

        /// <summary>
        /// Tries to resolve the given method to a top-level function.
        /// </summary>
        /// <param name="method">The method to resolve.</param>
        /// <param name="data">The resolved data (if any).</param>
        /// <returns>True, if the requested function could be resolved.</returns>
        public bool TryGetData(MethodHandle method, out T data)
        {
            if (!methods.TryGetValue(method, out int index))
            {
                data = default;
                return false;
            }
            else
            {
                data = dataList[index];
                return true;
            }
        }

        /// <summary>
        /// Registers the handle with the given data object.
        /// </summary>
        /// <param name="handle">The function handle.</param>
        /// <param name="data">The data object to register.</param>
        public void Register(MethodHandle handle, T data)
        {
            Debug.Assert(!handle.IsEmpty, "Invalid handle");
            Debug.Assert(data != null, "Invalid data");
            if (methods.TryGetValue(handle, out int index))
            {
                dataList[index] = data;
            }
            else
            {
                index = dataList.Count;
                dataList.Add(data);
                methods[handle] = index;
            }

            var source = data.Source;
            if (source != null)
                managedMethods[source] = handle;
        }

        /// <summary>
        /// Converts this mapping object into an array.
        /// </summary>
        /// <returns>The array.</returns>
        public T[] ToArray() => dataList.ToArray();

        /// <summary>
        /// Converts this mapping object into an immutable array.
        /// </summary>
        /// <returns>The immutable array.</returns>
        public ImmutableArray<T> ToImmutableArray()
        {
            var builder = ImmutableArray.CreateBuilder<T>(Count);
            foreach (var value in dataList)
                builder.Add(value);
            return builder.MoveToImmutable();
        }

        /// <summary>
        /// Clears all contained functions.
        /// </summary>
        public void Clear()
        {
            managedMethods.Clear();
            methods.Clear();
            dataList.Clear();
        }

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that enumerates all stored values.
        /// </summary>
        /// <returns>An enumerator that enumerates all stored values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(dataList);

        #endregion
    }
}
