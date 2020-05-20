// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Duplicates.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses.Duplicates
{
    /// <summary>
    /// Represents a type-constraint based specification of duplicate items.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public interface IDuplicates<T>
    {
        /// <summary>
        /// Returns true if duplicate entries are allowed.
        /// </summary>
        bool AllowDuplicates { get; }

        /// <summary>
        /// Adds an already visited item to the target list.
        /// </summary>
        /// <param name="target">The target list.</param>
        /// <param name="item">The item to add.</param>
        void AddAlreadyVisitedItem<TTarget>(TTarget target, T item)
            where TTarget : ICollection<T>;
    }

    /// <summary>
    /// Represents no duplicate items in a collection.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public readonly struct NoDuplicates<T> : IDuplicates<T>
    {
        /// <summary>
        /// Returns false.
        /// </summary>
        public bool AllowDuplicates => false;

        /// <summary>
        /// Adds no item to the target list.
        /// </summary>
        /// <param name="target">The target list.</param>
        /// <param name="item">The item to ignore.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAlreadyVisitedItem<TTarget>(TTarget target, T item)
            where TTarget : ICollection<T>
        { }
    }

    /// <summary>
    /// Adds already visited nodes to the post oder list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public readonly struct CanHaveDuplicates<T> : IDuplicates<T>
    {
        /// <summary>
        /// Returns false.
        /// </summary>
        public bool AllowDuplicates => false;

        /// <summary>
        /// Adds the given item to the target list.
        /// </summary>
        /// <param name="target">The target list.</param>
        /// <param name="item">The item to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAlreadyVisitedItem<TTarget>(TTarget target, T item)
            where TTarget : ICollection<T> =>
            target.Add(item);
    }
}
