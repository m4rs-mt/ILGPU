// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineList.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Util
{
    /// <summary>
    /// An inline array list that has to be passed by reference to avoid unnecessary
    /// heap allocations.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [SuppressMessage(
        "Usage",
        "CA2231:Overload operator equals on overriding value type Equals",
        Justification = "Overloading operators is not appropriate on this structure")]
    public struct InlineList<T>
    {
        #region Static

        /// <summary>
        /// An empty inline list.
        /// </summary>
        public static readonly InlineList<T> Empty = new InlineList<T>(0);

        /// <summary>
        /// Creates a new inline list with the given capacity and storage capacity.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <returns>The new inline list.</returns>
        public static InlineList<T> Create(int capacity) =>
            new InlineList<T>(capacity);

        /// <summary>
        /// Creates a new inline list with the given item.
        /// </summary>
        /// <param name="item">The first item.</param>
        /// <returns>The created inline list.</returns>
        public static InlineList<T> Create(T item)
        {
            var result = Create(1);
            result.Add(item);
            return result;
        }

        /// <summary>
        /// Creates a new inline list with the given items.
        /// </summary>
        /// <param name="item1">The first item.</param>
        /// <param name="item2">The second item.</param>
        /// <returns>The created inline list.</returns>
        public static InlineList<T> Create(T item1, T item2)
        {
            var result = Create(2);
            result.Add(item1);
            result.Add(item2);
            return result;
        }

        /// <summary>
        /// Creates a new inline list from the given list.
        /// </summary>
        /// <typeparam name="TList">The list type.</typeparam>
        /// <param name="list">The source list.</param>
        /// <returns>The new inline list.</returns>
        public static InlineList<T> Create<TList>(TList list)
            where TList : IReadOnlyList<T>
        {
            var result = Create(list.Count);
            for (int i = 0, e = list.Count; i < e; ++i)
                result.Add(list[i]);
            return result;
        }

        #endregion

        #region Instance

        private T[] items;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InlineList(int capacity)
        {
            items = capacity < 1
                ? Array.Empty<T>()
                : new T[capacity];
            Count = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of items.
        /// </summary>
        public int Count
        {
            readonly get;
            private set;
        }

        /// <summary>
        /// The total capacity.
        /// </summary>
        public readonly int Capacity => items.Length;

        /// <summary>
        /// Returns a reference to the i-th item.
        /// </summary>
        /// <param name="index">The item index.</param>
        /// <returns>The item reference.</returns>
        public readonly ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(index >= 0 && index < Count, "Index out of range");
                return ref items[index];
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ensures that at least a single item can be stored.
        /// </summary>
        private void EnsureCapacity()
        {
            if (Count + 1 > Capacity)
                Reserve(Math.Max(Capacity * 2, 2));
        }

        /// <summary>
        /// Ensures that this list as at least the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to ensure.</param>
        public void Reserve(int capacity)
        {
            Debug.Assert(capacity > 0, "Invalid capacity");
            if (Capacity >= capacity)
                return;

            // Increase capacity
            var newItems = new T[capacity];
            Array.Copy(items, newItems, items.Length);
            items = newItems;
        }

        /// <summary>
        /// Adds the given item to this list.
        /// </summary>
        /// <param name="item">The item to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            EnsureCapacity();
            items[Count] = item;
            ++Count;
        }

        /// <summary>
        /// Adds the given items to this list.
        /// </summary>
        /// <typeparam name="TList">The list type.</typeparam>
        /// <param name="list">The source list.</param>
        public void AddRange<TList>(TList list)
            where TList : IReadOnlyList<T>
        {
            if (list.Count < 1)
                return;
            Reserve(Count + list.Count);
            for (int i = 0, e = list.Count; i < e; ++i)
                Add(list[i]);
        }

        /// <summary>
        /// Adds the given items to this list.
        /// </summary>
        /// <param name="span">The source span.</param>
        public void AddRange(in ReadOnlySpan<T> span)
        {
            if (span.Length < 1)
                return;
            Reserve(Count + span.Length);
            foreach (var item in span)
                Add(item);
        }

        /// <summary>
        /// Clears all items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            items = Array.Empty<T>();
            Count = 0;
        }

        /// <summary>
        /// Resizes the current list to have a sufficient capacity for all items while
        /// settings the number of elements to <paramref name="count"/>.
        /// </summary>
        /// <param name="count">The desired number of elements.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int count)
        {
            Reserve(count);
            Count = count;
        }

        /// <summary>
        /// Returns true if the given item is contained in this list.
        /// </summary>
        /// <param name="item">The item to look for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>True, if the given item contained in this list.</returns>
        public readonly bool Contains<TComparer>(T item, TComparer comparer)
            where TComparer : IEqualityComparer<T> =>
            IndexOf(item, comparer) >= 0;

        /// <summary>
        /// Copies the internally stored items to the given array.
        /// </summary>
        /// <param name="array">The target array to copy to.</param>
        /// <param name="arrayIndex">The base index.</param>
        public readonly void CopyTo(T[] array, int arrayIndex) =>
            Array.Copy(items, 0, array, arrayIndex, Count);

        /// <summary>
        /// Inserts the given item at the specified index.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        /// <param name="index">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(T item, int index)
        {
            EnsureCapacity();
            for (int i = Count; i >= index; --i)
                items[i] = items[i - 1];
            items[index] = item;
            ++Count;
        }

        /// <summary>
        /// Returns the index of the given item.
        /// </summary>
        /// <param name="item">The item to look for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>The index of the item or -1.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int IndexOf<TComparer>(T item, TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            for (int i = 0, e = Count; i < e; ++i)
            {
                if (comparer.Equals(items[i], item))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Removes the given item from the list.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>True, if the item could be removed from the list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove<TComparer>(T item, TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            int index = IndexOf(item, comparer);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Removes all items that match from the list.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="comparer">The comparer to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAll<TComparer>(T item, TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            int index = IndexOf(item, comparer);
            while (index >= 0)
            {
                RemoveAt(index);
                index = IndexOf(item, comparer);
            }
        }

        /// <summary>
        /// Removes the item with the specified index.
        /// </summary>
        /// <param name="index">The item index.</param>
        public void RemoveAt(int index)
        {
            for (int i = index, e = Count - 1; i < e; ++i)
                items[i] = items[i + 1];
            --Count;
        }

        /// <summary>
        /// Pops an element from the back of this list.
        /// </summary>
        public T Pop()
        {
            var element = items[Count - 1];
            --Count;
            return element;
        }

        /// <summary>
        /// Reverses all items in this list.
        /// </summary>
        public void Reverse() => Array.Reverse(items);

        /// <summary>
        /// Moves the current items to the given target list.
        /// </summary>
        /// <param name="list">The target list to move to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveTo(ref InlineList<T> list)
        {
            list.items = items;
            list.Count = Count;

            Clear();
        }

        /// <summary>
        /// Copies all items to the given target list.
        /// </summary>
        /// <param name="list">The target list.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ref InlineList<T> list)
        {
            Array.Resize(ref list.items, Count);
            list.Count = Count;
            CopyTo(list.items, 0);
        }

        /// <summary>
        /// Clones this inline list.
        /// </summary>
        /// <returns>A clone of the current inline list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly InlineList<T> Clone()
        {
            var result = Create(Count);
            CopyTo(ref result);
            return result;
        }

        /// <summary>
        /// Slices a sub inline list out of this one.
        /// </summary>
        /// <param name="startIndex">The start index to begin slicing.</param>
        /// <param name="count">The number of elements to slice.</param>
        /// <returns>The new inline list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly InlineList<T> Slice(int startIndex, int count)
        {
            var result = Create(count);
            SliceTo(startIndex, count, ref result);
            return result;
        }

        /// <summary>
        /// Slices a sub inline list out of this one into the given one.
        /// </summary>
        /// <param name="startIndex">The start index to begin slicing.</param>
        /// <param name="count">The number of elements to slice.</param>
        /// <param name="list">The target list.</param>
        public readonly void SliceTo(
            int startIndex,
            int count,
            ref InlineList<T> list)
        {
            list.Reserve(Math.Max(list.Capacity, count));
            Array.Copy(items, startIndex, list.items, 0, count);
            list.Count = Math.Max(list.Count, count);
        }

        /// <summary>
        /// Returns true if the given list is equal to the current list.
        /// </summary>
        /// <param name="other">The other list.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>True, if the given list is equal to the current list.</returns>
        public readonly bool Equals<TComparer>(InlineList<T> other, TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            if (other.Count != Count)
                return false;
            for (int i = 0; i < Count; ++i)
            {
                if (!comparer.Equals(items[i], other.items[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Converts this inline list into a span.
        /// </summary>
        /// <returns>The span.</returns>
        public readonly Span<T> AsSpan() => new Span<T>(items, 0, Count);

        /// <summary>
        /// Converts this inline list into a read-only span.
        /// </summary>
        /// <returns>The read-only span.</returns>
        public readonly ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();

        /// <summary>
        /// Returns an enumerator to enumerate all items in this list.
        /// </summary>
        /// <returns>The enumerator.</returns>
        /// <remarks>
        /// CAUTION: iterating over this list can be dangerous, as the underlying inline
        /// list might change and this instance is a structure value.
        /// </remarks>
        public readonly ReadOnlySpan<T>.Enumerator GetEnumerator() =>
            AsReadOnlySpan().GetEnumerator();

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this list.
        /// </summary>
        /// <returns>The string representation of this list.</returns>
        public readonly string ToString<TFormatter>(TFormatter formatter)
            where TFormatter : InlineList.IFormatter<T> =>
            AsReadOnlySpan().ToString(formatter);

        /// <summary>
        /// Returns the string representation of this list.
        /// </summary>
        /// <returns>The string representation of this list.</returns>
        public readonly override string ToString() =>
            ToString(new InlineList.DefaultFormatter<T>());

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given list into a span.
        /// </summary>
        /// <param name="list">The list to convert.</param>
        public static explicit operator Span<T>(InlineList<T> list) =>
            new Span<T>(list.items, 0, list.Count);

        /// <summary>
        /// Converts the given list into a read-only span.
        /// </summary>
        /// <param name="list">The list to convert.</param>
        public static implicit operator ReadOnlySpan<T>(InlineList<T> list) =>
            new ReadOnlySpan<T>(list.items, 0, list.Count);

        #endregion
    }

    /// <summary>
    /// Inline list utility methods.
    /// </summary>
    public static class InlineList
    {
        #region Nested Types

        /// <summary>
        /// An abstract value formatter for inline lists.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public interface IFormatter<T>
        {
            /// <summary>
            /// Formats the given item.
            /// </summary>
            /// <param name="item">The item to format.</param>
            /// <returns>The formatted string representation.</returns>
            string Format(T item);
        }

        /// <summary>
        /// The default formatter that calls the <see cref="object.ToString()"/> method.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public readonly struct DefaultFormatter<T> : IFormatter<T>
        {
            /// <summary>
            /// Formats the given item using <see cref="object.ToString()"/>.
            /// </summary>
            /// <param name="item">The item to format.</param>
            /// <returns>The default string representation.</returns>
            public readonly string Format(T item) => item.ToString();
        }

        /// <summary>
        /// An abstract predicate interface.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public interface IPredicate<T>
        {
            /// <summary>
            /// Applies this predicate to the given item.
            /// </summary>
            /// <param name="item">The item to apply the predicate to.</param>
            /// <returns>
            /// True, if the predicate implementation evaluates to true.
            /// </returns>
            bool Apply(T item);
        }

        /// <summary>
        /// A predicate that always returns true.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public readonly struct TruePredicate<T> : IPredicate<T>
        {
            /// <summary>
            /// Returns always true.
            /// </summary>
            /// <param name="item">The item to apply the predicate to.</param>
            /// <returns>True.</returns>
            public readonly bool Apply(T item) => true;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a new span that does not contain the given element.
        /// </summary>
        /// <param name="span">The span that might contain the given item.</param>
        /// <param name="element">The item to exclude for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>A span that does not contain the given element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<T> ExceptAll<T, TComparer>(
            this ReadOnlySpan<T> span,
            T element,
            TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            if (!span.Contains(element, comparer))
                return span;

            var newSuccessors = span.ToInlineList();
            newSuccessors.RemoveAll(element, comparer);
            return newSuccessors;
        }

        /// <summary>
        /// Returns true if the given item is contained in this span.
        /// </summary>
        /// <param name="span">The span that might contain the given item.</param>
        /// <param name="element">The item to look for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>True, if the given item contained in this list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T, TComparer>(
            this ReadOnlySpan<T> span,
            T element,
            TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            foreach (var item in span)
            {
                if (comparer.Equals(item, element))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the predicate evaluates to true for any item.
        /// </summary>
        /// <param name="span">The span that might contain the given item.</param>
        /// <param name="predicate">The predicate instance.</param>
        /// <returns>True, if the predicate evaluates to true for any item.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Any<T, TPredicate>(
            this ReadOnlySpan<T> span,
            TPredicate predicate)
            where TPredicate : IPredicate<T>
        {
            foreach (var item in span)
            {
                if (predicate.Apply(item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a new inline list from the given span.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="span">The span instance.</param>
        /// <returns>The created inline list.</returns>
        public static InlineList<T> ToInlineList<T>(this ReadOnlySpan<T> span)
        {
            var result = InlineList<T>.Empty;
            span.CopyTo(ref result);
            return result;
        }

        /// <summary>
        /// Converts the given span into a <see cref="HashSet{T}"/>.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="span">The span instance.</param>
        public static HashSet<T> ToSet<T>(this ReadOnlySpan<T> span) =>
            span.ToSet(new TruePredicate<T>());

        /// <summary>
        /// Converts the given span into a <see cref="HashSet{T}"/> that contains all
        /// all elements for which the given predicate evaluates to true.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="span">The span instance.</param>
        /// <param name="predicate">The predicate instance.</param>
        /// <returns>The created set.</returns>
        public static HashSet<T> ToSet<T, TPredicate>(
            this ReadOnlySpan<T> span,
            TPredicate predicate)
            where TPredicate : IPredicate<T>
        {
            var result = new HashSet<T>();
            foreach (var item in span)
            {
                if (predicate.Apply(item))
                    result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Copies the items from the given span to the inline list.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="span">The span instance.</param>
        /// <param name="list">A reference to the inline list.</param>
        public static void CopyTo<T>(
            this ReadOnlySpan<T> span,
            ref InlineList<T> list) =>
            list.AddRange(span);

        /// <summary>
        /// Returns the string representation of the given span.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TFormatter">The formatter type.</typeparam>
        /// <param name="span">The span instance.</param>
        /// <param name="formatter">The custom formatter.</param>
        /// <returns>The string representation of this list.</returns>
        public static string ToString<T, TFormatter>(
            this ReadOnlySpan<T> span,
            TFormatter formatter)
            where TFormatter : IFormatter<T>
        {
            if (span.Length < 1)
            {
                return string.Empty;
            }
            else if (span.Length < 3)
            {
                string result = formatter.Format(span[0]);
                if (span.Length > 1)
                    result += ", " + formatter.Format(span[1]);
                if (span.Length > 2)
                    result += ", " + formatter.Format(span[2]);
                return result;
            }
            else
            {
                var result = new StringBuilder();
                for (int i = 0, e = span.Length; i < e; ++i)
                {
                    result.Append(formatter.Format(span[i]));
                    if (i + 1 < e)
                        result.Append(", ");
                }
                return result.ToString();
            }
        }

        #endregion
    }
}
