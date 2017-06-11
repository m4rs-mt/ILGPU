// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: IIndex.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU
{
    /// <summary>
    /// Represents the type of index.
    /// </summary>
    public enum IndexType
    {
        /// <summary>
        /// Represents no compatible index type.
        /// </summary>
        None = 0,

        /// <summary>
        /// Represents a 1D index.
        /// </summary>
        Index1D = 1,

        /// <summary>
        /// Represents a 2D index.
        /// </summary>
        Index2D = 2,

        /// <summary>
        /// Represents a 3D index.
        /// </summary>
        Index3D = 3,

        /// <summary>
        /// Represents a grouped 1D index.
        /// </summary>
        GroupedIndex1D = 4,

        /// <summary>
        /// Represents a grouped 2D index.
        /// </summary>
        GroupedIndex2D = 5,

        /// <summary>
        /// Represents a grouped 3D index.
        /// </summary>
        GroupedIndex3D = 6,
    }

    /// <summary>
    /// Contains utility functions for handling index types.
    /// </summary>
    static class IndexTypeExtensions
    {
        /// <summary>
        /// Resolves the managed type of an index for a given index type.
        /// </summary>
        /// <param name="indexType">The index type.</param>
        /// <returns>The resolved managed index type..</returns>
        public static Type GetManagedIndexType(this IndexType indexType)
        {
            switch (indexType)
            {
                case IndexType.Index1D:
                    return typeof(Index);
                case IndexType.Index2D:
                    return typeof(Index2);
                case IndexType.Index3D:
                    return typeof(Index3);
                case IndexType.GroupedIndex1D:
                    return typeof(GroupedIndex);
                case IndexType.GroupedIndex2D:
                    return typeof(GroupedIndex2);
                case IndexType.GroupedIndex3D:
                    return typeof(GroupedIndex3);
                default:
                    throw new ArgumentOutOfRangeException(nameof(indexType));
            }
        }

        /// <summary>
        /// Tries to resolve an index type based on the given .Net type.
        /// </summary>
        /// <param name="indexType">The managed .Net index type.</param>
        /// <returns>The resolved index type or none.</returns>
        public static IndexType GetIndexType(this Type indexType)
        {
            if (indexType == typeof(Index))
                return IndexType.Index1D;
            else if (indexType == typeof(Index2))
                return IndexType.Index2D;
            else if (indexType == typeof(Index3))
                return IndexType.Index3D;
            else if (indexType == typeof(GroupedIndex))
                return IndexType.GroupedIndex1D;
            else if (indexType == typeof(GroupedIndex2))
                return IndexType.GroupedIndex2D;
            else if (indexType == typeof(GroupedIndex3))
                return IndexType.GroupedIndex3D;
            return IndexType.None;
        }

        /// <summary>
        /// Tries to resolve an ungrouped index type.
        /// An ungrouped index type is either <see cref="IndexType.None"/>,
        /// <see cref="IndexType.Index1D"/>, <see cref="IndexType.Index2D"/> or
        /// <see cref="IndexType.Index3D"/>.
        /// </summary>
        /// <param name="indexType">The index type.</param>
        /// <returns>The resolved index type or none.</returns>
        public static IndexType GetUngroupedIndexType(this IndexType indexType)
        {
            switch (indexType)
            {
                case IndexType.GroupedIndex1D:
                    return IndexType.Index1D;
                case IndexType.GroupedIndex2D:
                    return IndexType.Index2D;
                case IndexType.GroupedIndex3D:
                    return IndexType.Index3D;
                default:
                    return indexType;
            }
        }

        /// <summary>
        /// Tries to resolve an ungrouped index type.
        /// An ungrouped index type is either <see cref="IndexType.None"/>,
        /// <see cref="IndexType.Index1D"/>, <see cref="IndexType.Index2D"/> or
        /// <see cref="IndexType.Index3D"/>.
        /// </summary>
        /// <param name="indexType">The managed .Net index type.</param>
        /// <returns>The resolved index type or none.</returns>
        public static IndexType GetUngroupedIndexType(this Type indexType)
        {
            return GetUngroupedIndexType(GetIndexType(indexType));
        }
    }

    /// <summary>
    /// Represents a basic index type.
    /// </summary>
    public interface IIndex
    {

        /// <summary>
        /// Returns the size represented by this index (eg. x, x * y or x * y * z).
        /// </summary>
        int Size { get; }
    }

    /// <summary>
    /// Represents an intrinsic index type.
    /// </summary>
    public interface IIntrinsicIndex : IIndex
    {
        /// <summary>
        /// Returns the current index type.
        /// </summary>
        IndexType IndexType { get; }
    }

    /// <summary>
    /// Represents a generic index type.
    /// </summary>
    /// <typeparam name="TIndex">The type of the generic index.</typeparam>
    public interface IGenericIndex<TIndex> : IEquatable<TIndex>, IComparable<TIndex>
        where TIndex : struct, IIndex
    {
        /// <summary>
        /// Returs true iff the current index is greater than or equal to 0 and
        /// is less than the given dimension.
        /// </summary>
        /// <param name="dimension">The dimension bounds.</param>
        /// <returns>True iff the current index is inside the given bounds.</returns>
        bool InBounds(TIndex dimension);

        /// <summary>
        /// Returs true iff the current index is greater than or equal to 0 and
        /// is less than or equal to the given dimension.
        /// </summary>
        /// <param name="dimension">The dimension bounds.</param>
        /// <returns>True iff the current index is inside the given bounds.</returns>
        bool InBoundsInclusive(TIndex dimension);

        /// <summary>
        /// Computes the linear index of this index by using the provided n-D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this index.</returns>
        int ComputeLinearIndex(TIndex dimension);

        /// <summary>
        /// Reconstructs an index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The lienar index.</param>
        /// <returns>The reconstructed index.</returns>
        TIndex ReconstructIndex(int linearIndex);

        /// <summary>
        /// Computes this + rhs.
        /// </summary>
        /// <param name="rhs">The right-hand side of the addition.</param>
        /// <returns>The added index.</returns>
        TIndex Add(TIndex rhs);

        /// <summary>
        /// Computes this - rhs.
        /// </summary>
        /// <param name="rhs">The right-hand side of the subtraction.</param>
        /// <returns>The subtracted index.</returns>
        TIndex Subtract(TIndex rhs);

        /// <summary>
        /// The given <paramref name="extent"/> describes a chunk of contiguous memory of elements with
        /// size <paramref name="elementSize"/>. The parameter <paramref name="newElementSize"/> describes
        /// the requested new element size. The result of this function is a new extent dimension that
        /// represents the given extent in the context of the new element size.
        /// </summary>
        /// <param name="extent">The current extent.</param>
        /// <param name="elementSize">The current element size in the scope of the current extent.</param>
        /// <param name="newElementSize">The new element size.</param>
        /// <returns>The adjusted extent to match the new element size.</returns>
        TIndex ComputedCastedExtent(TIndex extent, int elementSize, int newElementSize);
    }

    /// <summary>
    /// Represents a grouped-index type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1040:AvoidEmptyInterfaces")]
    public interface IGroupedIndex : IIndex
    { }

    /// <summary>
    /// Represents a grouped-index type.
    /// </summary>
    public interface IGroupedIndex<TIndex> : IGroupedIndex
        where TIndex : IIndex, IEquatable<TIndex>
    {
        /// <summary>
        /// Returns the global block idx.
        /// </summary>
        TIndex GridIdx { get; }

        /// <summary>
        /// Returns the lock thread idx.
        /// </summary>
        TIndex GroupIdx { get; }
    }
}
