﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: UseDistribution.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// An analysis class to resolve information about the global use-relation
    /// within a given context.
    /// </summary>
    /// <remarks>
    /// This class is typically used for internal debugging and tracking of memory
    /// allocations.
    /// </remarks>
    public sealed class UseDistribution
    {
        #region Instance

        /// <summary>
        /// Constructs a new use distribution.
        /// </summary>
        /// <param name="context">The target context.</param>
        public UseDistribution(IRContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var groupedUses = new Dictionary<int, int>();
            var usesPerType = new Dictionary<Type, (int, int)>();

            foreach (var method in context.Methods)
            {
                foreach (Value value in method.Blocks.Values)
                {
                    if (!groupedUses.TryGetValue(value.AllNumUses, out int count))
                        count = 0;
                    groupedUses[value.AllNumUses] = count + 1;

                    var type = value.GetType();
                    if (!usesPerType.TryGetValue(type, out var entry))
                        entry = (0, 0);

                    usesPerType[type] =
                        (IntrinsicMath.Max(value.AllNumUses, entry.Item1),
                        entry.Item2 + 1);
                }
            }

            var groupedUsesList = new List<(int, int)>(groupedUses.Count);
            foreach (var entry in groupedUses)
                groupedUsesList.Add((entry.Key, entry.Value));
            groupedUsesList.Sort((x, y) => y.Item1.CompareTo(x.Item1));
            Uses = groupedUsesList.ToImmutableArray();

            var groupedUsesPerTypeList = new List<(int, Type, int)>(usesPerType.Count);
            foreach (var entry in usesPerType)
            {
                groupedUsesPerTypeList.Add(
                    (entry.Value.Item1, entry.Key, entry.Value.Item2));
            }
            groupedUsesPerTypeList.Sort((x, y) => y.Item1.CompareTo(x.Item1));

            Uses = groupedUsesList.ToImmutableArray();
            UsesPerType = groupedUsesPerTypeList.ToImmutableArray();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the use distribution of all global nodes.
        /// </summary>
        /// <remarks>Tuple layout: (number of uses, number of nodes).</remarks>
        public ImmutableArray<(int, int)> Uses { get; }

        /// <summary>
        /// Returns the use distribution of all node types.
        /// </summary>
        /// <remarks>
        /// Tuple layout: (max number of uses, type, number of nodes with this type).
        /// </remarks>
        public ImmutableArray<(int, Type, int)> UsesPerType { get; }

        #endregion
    }
}
