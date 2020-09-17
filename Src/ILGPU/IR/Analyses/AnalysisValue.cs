// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AnalysisValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An abstract analysis value context.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    public interface IAnalysisValueContext<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Returns the analysis value associated with the given value.
        /// </summary>
        /// <param name="value">The source value to lookup.</param>
        /// <returns>The parent value.</returns>
        AnalysisValue<T> this[Value value] { get; }
    }

    /// <summary>
    /// An analysis value to encapsulate static program analysis values.
    /// </summary>
    /// <typeparam name="T">The underlying element type.</typeparam>
    /// <remarks>
    /// This type encapsulates a general <see cref="Data"/> element that represents
    /// accumulated analysis information for the whole object. Furthermore, it stores
    /// additional fine-grained information about each child element in the case of
    /// structure values. This improves the overall program analysis precision.
    /// </remarks>
    public readonly struct AnalysisValue<T> :
        IEquatable<AnalysisValue<T>>
        where T : IEquatable<T>
    {
        #region Instance

        private readonly T[] childData;

        /// <summary>
        /// Constructs a new analysis value with the given data value.
        /// </summary>
        /// <param name="data">The accumulated data value.</param>
        public AnalysisValue(T data)
            : this(data, Array.Empty<T>())
        { }

        /// <summary>
        /// Constructs a new analysis value with different data values for each child.
        /// </summary>
        /// <param name="data">The accumulated data value.</param>
        /// <param name="childArray">All child data values.</param>
        public AnalysisValue(T data, T[] childArray)
        {
            Data = data;
            childData = childArray ?? Array.Empty<T>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying data value.
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// Returns the number of child elements.
        /// </summary>
        public readonly int NumFields => childData.Length;

        /// <summary>
        /// Returns the i-th child data element.
        /// </summary>
        /// <param name="index">The child index.</param>
        /// <returns></returns>
        public readonly T this[int index] => childData[index];

        #endregion

        #region Methods

        /// <summary>
        /// Clones the internal child-data array into a new one.
        /// </summary>
        /// <returns>The cloned child-data array.</returns>
        public readonly T[] CloneChildData()
        {
            var newChildData = new T[NumFields];
            Array.Copy(childData, newChildData, NumFields);
            return newChildData;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given value is equal to the current one.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>True, if the given value is equal to the current one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(AnalysisValue<T> other)
        {
            if (!Data.Equals(other.Data) || NumFields != other.NumFields)
                return false;
            for (int i = 0, e = NumFields; i < e; ++i)
            {
                if (!childData[i].Equals(other.childData[i]))
                    return false;
            }
            return true;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current value.</returns>
        public readonly override bool Equals(object obj) =>
            obj is AnalysisValue<T> value && Equals(value);

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode() => Data.GetHashCode() ^ NumFields;

        /// <summary>
        /// Returns the string representation of this value.
        /// </summary>
        /// <returns>The string representation of this value.</returns>
        public readonly override string ToString() =>
            childData.Length > 0
            ? $"{Data} [{string.Join(", ", childData)}]"
            : Data.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second value are the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are the same.</returns>
        public static bool operator ==(
            AnalysisValue<T> first,
            AnalysisValue<T> second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second value are not the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are not the same.</returns>
        public static bool operator !=(
            AnalysisValue<T> first,
            AnalysisValue<T> second) =>
            !(first == second);

        #endregion
    }

    /// <summary>
    /// Helper methods for the structure <see cref="AnalysisValueMapping{T}"/>.
    /// </summary>
    public static class AnalysisValueMapping
    {
        /// <summary>
        /// Creates a new analysis mapping instance.
        /// </summary>
        /// <typeparam name="T">The target mapping type.</typeparam>
        /// <returns>The initialized analysis mapping instance.</returns>
        public static AnalysisValueMapping<T> Create<T>()
            where T : struct, IEquatable<T> =>
            new AnalysisValueMapping<T>(new Dictionary<Value, AnalysisValue<T>>());
    }

    /// <summary>
    /// Maps <see cref="Value"/> instances to <see cref="AnalysisValue{T}"/> instances
    /// specialized using the user-defined type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target mapping type.</typeparam>
    [SuppressMessage(
        "Naming",
        "CA1710:Identifiers should have correct suffix",
        Justification = "The collection ends in mapping")]
    public readonly struct AnalysisValueMapping<T> :
        IReadOnlyDictionary<Value, AnalysisValue<T>>
        where T : struct, IEquatable<T>
    {
        #region Instance

        private readonly Dictionary<Value, AnalysisValue<T>> mapping;

        /// <summary>
        /// Constructs a new value mapping using the given dictionary.
        /// </summary>
        /// <param name="data">The underlying dictionary to use.</param>
        public AnalysisValueMapping(Dictionary<Value, AnalysisValue<T>> data)
        {
            mapping = data ?? throw new ArgumentNullException(nameof(data));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Lookups the given key in this map.
        /// </summary>
        /// <param name="key">The key to lookup.</param>
        /// <returns>The resolved analysis value.</returns>
        public AnalysisValue<T> this[Value key]
        {
            readonly get => mapping[key];
            internal set => mapping[key] = value;
        }

        /// <summary cref="IReadOnlyDictionary{TKey, TValue}.Keys" />
        IEnumerable<Value> IReadOnlyDictionary<Value, AnalysisValue<T>>.Keys =>
            mapping.Keys;

        /// <summary cref="IReadOnlyDictionary{TKey, TValue}.Values" />
        IEnumerable<AnalysisValue<T>>
            IReadOnlyDictionary<Value, AnalysisValue<T>>.Values =>
            mapping.Values;

        /// <summary>
        /// Returns the number of elements in this mapping.
        /// </summary>
        public readonly int Count => mapping.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given key is contained in this map.
        /// </summary>
        /// <param name="key">The key to lookup.</param>
        /// <returns>True, if the given key is contained in this map.</returns>
        public readonly bool ContainsKey(Value key) =>
            mapping.ContainsKey(key);

        /// <summary>
        /// Tries to get map the given key to a stored value.
        /// </summary>
        /// <param name="key">The key to lookup.</param>
        /// <param name="value">The resolved value (if any).</param>
        /// <returns>True, if the given key could be found.</returns>
        public readonly bool TryGetValue(Value key, out AnalysisValue<T> value) =>
            mapping.TryGetValue(key, out value);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all items in this mapping.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly Dictionary<Value, AnalysisValue<T>>.Enumerator GetEnumerator() =>
            mapping.GetEnumerator();

        /// <summary cref="IEnumerable{T}.GetEnumerator" />
        IEnumerator<KeyValuePair<Value, AnalysisValue<T>>>
            IEnumerable<KeyValuePair<Value, AnalysisValue<T>>>.GetEnumerator() =>
            GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator" />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
