// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IIndex.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        /// Represents a generic kernel config.
        /// </summary>
        KernelConfig = 4,
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
                    return typeof(Index1);
                case IndexType.Index2D:
                    return typeof(Index2);
                case IndexType.Index3D:
                    return typeof(Index3);
                case IndexType.KernelConfig:
                    return typeof(KernelConfig);
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
#pragma warning disable CS0618 // Type or member is obsolete
            // TODO: replace the additional check once the Index type has
            // been removed from the project
            if (indexType == typeof(Index1) || indexType == typeof(Index))
                return IndexType.Index1D;
            else if (indexType == typeof(Index2))
                return IndexType.Index2D;
            else if (indexType == typeof(Index3))
                return IndexType.Index3D;
            else if (indexType == typeof(KernelConfig))
                return IndexType.KernelConfig;
            return IndexType.None;
#pragma warning restore CS0618 // Type or member is obsolete
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
        /// Returns true if the current index is greater than or equal to 0 and
        /// is less than the given dimension.
        /// </summary>
        /// <param name="dimension">The dimension bounds.</param>
        /// <returns>True if the current index is inside the given bounds.</returns>
        bool InBounds(TIndex dimension);

        /// <summary>
        /// Returns true if the current index is greater than or equal to 0 and
        /// is less than or equal to the given dimension.
        /// </summary>
        /// <param name="dimension">The dimension bounds.</param>
        /// <returns>True if the current index is inside the given bounds.</returns>
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
        /// <param name="linearIndex">The linear index.</param>
        /// <returns>The reconstructed index.</returns>
        TIndex ReconstructIndex(int linearIndex);

        /// <summary>
        /// Computes this + right-hand side.
        /// </summary>
        /// <param name="rhs">The right-hand side of the addition.</param>
        /// <returns>The added index.</returns>
        TIndex Add(TIndex rhs);

        /// <summary>
        /// Computes this - right-hand side.
        /// </summary>
        /// <param name="rhs">The right-hand side of the subtraction.</param>
        /// <returns>The subtracted index.</returns>
        TIndex Subtract(TIndex rhs);

        /// <summary>
        /// The given <paramref name="extent"/> describes a chunk of contiguous memory
        /// of elements with size <paramref name="elementSize"/>. The parameter
        /// <paramref name="newElementSize"/> describes the requested new element size.
        /// The result of this function is a new extent dimension that represents the
        /// given extent in the context of the new element size.
        /// </summary>
        /// <param name="extent">The current extent.</param>
        /// <param name="elementSize">
        /// The current element size in the scope of the current extent.
        /// </param>
        /// <param name="newElementSize">The new element size.</param>
        /// <returns>The adjusted extent to match the new element size.</returns>
        TIndex ComputedCastedExtent(TIndex extent, int elementSize, int newElementSize);
    }
}
