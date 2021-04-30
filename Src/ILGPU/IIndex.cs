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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        /// Represents a 1D index.
        /// </summary>
        LongIndex1D = 4,

        /// <summary>
        /// Represents a 2D index.
        /// </summary>
        LongIndex2D = 5,

        /// <summary>
        /// Represents a 3D index.
        /// </summary>
        LongIndex3D = 6,

        /// <summary>
        /// Represents a generic kernel config.
        /// </summary>
        KernelConfig = 7,
    }

    /// <summary>
    /// An internal attribute to specify the index type of a custom structure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    sealed class IndexTypeAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new attribute instance.
        /// </summary>
        /// <param name="indexType">The index type.</param>
        public IndexTypeAttribute(IndexType indexType)
        {
            IndexType = indexType;
        }

        /// <summary>
        /// Returns the associated index type.
        /// </summary>
        public IndexType IndexType { get; }
    }

    /// <summary>
    /// Contains utility functions for handling index types.
    /// </summary>
    public static class IndexTypeExtensions
    {
        /// <summary>
        /// An internal mapping of the <see cref="IndexType"/> values to managed types.
        /// </summary>
        private static readonly Type[] ManagedIndexTypes =
        {
            null,
            typeof(Index1D),
            typeof(Index2D),
            typeof(Index3D),
            typeof(LongIndex1D),
            typeof(LongIndex2D),
            typeof(LongIndex3D),
            typeof(KernelConfig)
        };

        /// <summary>
        /// A reference to the 32-bit index linearization method.
        /// </summary>
        private static readonly MethodInfo ViewLinearIndex32Method =
            typeof(IndexTypeExtensions).GetMethod(
                nameof(ViewLinearIndex32),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// A reference to the 64-bit index linearization method.
        /// </summary>
        private static readonly MethodInfo ViewLinearIndex64Method =
            typeof(IndexTypeExtensions).GetMethod(
                nameof(ViewLinearIndex64),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Asserts that the given long range can be accessed using a 32-bit integer.
        /// </summary>
        /// <param name="index">The long value range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertIntIndex(long index) =>
            Trace.Assert(
                index >= int.MinValue & index <= int.MaxValue,
                "64-bit index expected");

        /// <summary>
        /// Asserts that the given long range can be expressed by using a 32-bit integer.
        /// </summary>
        /// <param name="range">The long value range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertIntIndexRange(long range) =>
            Trace.Assert(
                range >= int.MinValue & range <= int.MaxValue,
                "32-bit index out of range");

        /// <summary>
        /// Returns a 32-bit integer size of the given index.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="index">The index type instance.</param>
        /// <returns>The 32-bit integer size of the given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIntSize<TIndex>(this TIndex index)
            where TIndex : struct, IIndex
        {
            long size = index.Size;
            AssertIntIndexRange(size);
            return (int)size;
        }

        /// <summary>
        /// Resolves the managed type of an index for a given index type.
        /// </summary>
        /// <param name="indexType">The index type.</param>
        /// <returns>The resolved managed index type..</returns>
        public static Type GetManagedIndexType(this IndexType indexType)
        {
            var resultType = ManagedIndexTypes[(int)indexType];
            return resultType ??
                throw new ArgumentOutOfRangeException(nameof(indexType));
        }

        /// <summary>
        /// Tries to resolve an index type based on the given .Net type.
        /// </summary>
        /// <param name="indexType">The managed .Net index type.</param>
        /// <returns>The resolved index type or none.</returns>
        public static IndexType GetIndexType(this Type indexType)
        {
            var attribute = indexType.GetCustomAttribute<IndexTypeAttribute>();
            return attribute is null ? IndexType.None : attribute.IndexType;
        }

        /// <summary>
        /// Returns true if the given type is a 64-bit index type.
        /// </summary>
        /// <param name="type">The managed .Net index type.</param>
        /// <returns>True, if the given index type is a 64-bit index type.</returns>
        public static bool IsLongIndex(this Type type)
        {
            switch (type.GetIndexType())
            {
                case IndexType.Index1D:
                case IndexType.Index2D:
                case IndexType.Index3D:
                    return false;
                case IndexType.LongIndex1D:
                case IndexType.LongIndex2D:
                case IndexType.LongIndex3D:
                    return true;
                default:
                    return typeof(IIntIndex).IsAssignableFrom(type);
            }
        }

        /// <summary>
        /// Returns true if the given type is a 64-bit index type.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <returns>True, if the given index type is a 64-bit index type.</returns>
        public static bool IsLongIndex<TIndex>()
            where TIndex : struct, IIndex =>
            typeof(TIndex).IsLongIndex();

        /// <summary>
        /// Gets the index linearization method that works either on 32-bit or on 64-bit
        /// values depending on the index type.
        /// </summary>
        /// <param name="indexType">The managed index type.</param>
        /// <param name="elementType">
        /// The managed element type of a particular view.
        /// </param>
        /// <returns>The managed 32-bit or 64-bit linearization method.</returns>
        public static MethodBase GetViewLinearIndexMethod(
            this Type indexType,
            Type elementType)
        {
            var genericTypeArgs = new Type[] { elementType, indexType };
            return indexType.IsLongIndex()
                ? ViewLinearIndex64Method.MakeGenericMethod(genericTypeArgs)
                : ViewLinearIndex32Method.MakeGenericMethod(genericTypeArgs);
        }

        /// <summary>
        /// Loads a linear element address using the given multi-dimensional indices.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="view">The source view.</param>
        /// <param name="index">The element index.</param>
        /// <param name="dimension">The dimension specifications.</param>
        /// <returns>A reference to the i-th element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref T ViewLinearIndex32<T, TIndex>(
            ArrayView<T> view,
            TIndex index,
            TIndex dimension)
            where T : unmanaged
            where TIndex : struct, IGenericIndex<TIndex>
        {
            Trace.Assert(
                index.InBounds(dimension),
                "Multidimensional index out of range");
            int linearIndex = index.ComputeLinearIndex(dimension);
            return ref view[linearIndex];
        }

        /// <summary>
        /// Loads a linear element address using the given multi-dimensional indices.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="view">The source view.</param>
        /// <param name="index">The element index.</param>
        /// <param name="dimension">The dimension specifications.</param>
        /// <returns>A reference to the i-th element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref T ViewLinearIndex64<T, TIndex>(
            ArrayView<T> view,
            TIndex index,
            TIndex dimension)
            where T : unmanaged
            where TIndex : struct, IGenericIndex<TIndex>
        {
            Trace.Assert(
                index.InBounds(dimension),
                "Multidimensional index out of range");
            long linearIndex = index.ComputeLongLinearIndex(dimension);
            return ref view[linearIndex];
        }
    }

    /// <summary>
    /// Represents a basic index type.
    /// </summary>
    public interface IIndex
    {
        /// <summary>
        /// Returns the size represented by this index (e.g. x, x * y or x * y * z).
        /// </summary>
        long Size { get; }
    }

    /// <summary>
    /// Represents a 32-bit index.
    /// </summary>
    public interface IIntIndex : IIndex
    {
        /// <summary>
        /// Returns the size represented by this index (e.g. x, x * y or x * y * z).
        /// </summary>
        new int Size { get; }
    }

    /// <summary>
    /// An abstract index that can be converted to or reconstructed from a linear value.
    /// </summary>
    /// <typeparam name="TIndex">The underlying index type.</typeparam>
    public interface ILinearizableIndex<TIndex> : IIndex
        where TIndex : struct, IIndex
    {
        /// <summary>
        /// Computes the linear index of this index by using the provided n-D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this index.</returns>
        int ComputeLinearIndex(TIndex dimension);

        /// <summary>
        /// Computes the linear index of this index by using the provided n-D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this index.</returns>
        long ComputeLongLinearIndex(TIndex dimension);

        /// <summary>
        /// Reconstructs an index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The linear index.</param>
        /// <returns>The reconstructed index.</returns>
        TIndex ReconstructIndex(int linearIndex);

        /// <summary>
        /// Reconstructs an index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The linear index.</param>
        /// <returns>The reconstructed index.</returns>
        TIndex ReconstructIndex(long linearIndex);
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
    public interface IGenericIndex<TIndex> :
        ILinearizableIndex<TIndex>,
        IEquatable<TIndex>
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
    }

    /// <summary>
    /// An integer register.
    /// </summary>
    /// <typeparam name="TIndex">The integer type.</typeparam>
    /// <typeparam name="TLongIndex">The long integer type.</typeparam>
    public interface IIntIndex<TIndex, TLongIndex> :
        IIntIndex,
        IIntrinsicIndex,
        IGenericIndex<TIndex>
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
    {
        /// <summary>
        /// Converts this index to a long integer index.
        /// </summary>
        /// <returns>The resulting long integer representation.</returns>
        TLongIndex ToLongIndex();
    }

    /// <summary>
    /// A long integer register.
    /// </summary>
    /// <typeparam name="TLongIndex">The long integer type.</typeparam>
    /// <typeparam name="TIndex">The integer type.</typeparam>
    public interface ILongIndex<TLongIndex, TIndex> :
        IIntrinsicIndex,
        IGenericIndex<TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
    {
        /// <summary>
        /// Converts this index to an integer index.
        /// </summary>
        /// <returns>The resulting integer representation.</returns>
        TIndex ToIntIndex();
    }
}
