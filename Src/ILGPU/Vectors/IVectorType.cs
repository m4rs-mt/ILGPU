// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IVectorType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Runtime;
using System;
using System.Numerics;

#if NET7_0_OR_GREATER

#pragma warning disable CA1000 // Do not declare static members on generic types
#pragma warning disable CA2225 // Friendly operator names

namespace ILGPU.Algorithms.Vectors
{
    /// <summary>
    /// An abstract numeric vector type with a number of elements.
    /// </summary>
    public interface IVectorType
    {
        /// <summary>
        /// Returns the vector length in terms of its number of elements.
        /// </summary>
        static abstract int Length { get; }
    }

    /// <summary>
    /// An abstract numeric vector type with a number of elements.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type.</typeparam>
    public interface IVectorType<TSelf> : IVectorType, INumberBase<TSelf>
        where TSelf : struct, IVectorType<TSelf>, INumberBase<TSelf>
    {
        /// <summary>
        /// Returns an invalid vector value used to track invalid number values.
        /// </summary>
        static abstract TSelf Invalid { get; }
        
        /// <summary>
        /// Computes the min value of both.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The min value.</returns>
        static abstract TSelf Min(TSelf first, TSelf second);

        /// <summary>
        /// Computes the max value of both.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The max value.</returns>
        static abstract TSelf Max(TSelf first, TSelf second);

        /// <summary>
        /// Clamps the given value.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>The clamped value.</returns>
        static abstract TSelf Clamp(
            TSelf value,
            TSelf min,
            TSelf max);
    }

    /// <summary>
    /// Represents a type-safe iterator over each vector element.
    /// </summary>
    /// <typeparam name="TNumericType">The vector type to iterate over.</typeparam>
    /// <typeparam name="TElementType">The underlying element type.</typeparam>
    public interface IVectorElementIterator<TNumericType, TElementType>
        where TNumericType : struct, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
    {
        /// <summary>
        /// Iterates over a current vector type instance while being invoked for each
        /// element type instance.
        /// </summary>
        /// <param name="value">The current element type.</param>
        /// <param name="index">The element index within the vector.></param>
        void Iterate(TElementType value, int index);
    }

    /// <summary>
    /// An abstract numeric vector type with a number of elements.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type.</typeparam>
    /// <typeparam name="TElementType">The underlying element type.</typeparam>
    public interface IVectorType<TSelf, TElementType> : IVectorType<TSelf>
        where TSelf :
        struct,
        IVectorType<TSelf, TElementType>,
        INumberBase<TSelf>
        where TElementType : unmanaged, INumber<TElementType>
    {
        /// <summary>
        /// Creates a random scalar instance falling into the range of min max.
        /// </summary>
        /// <param name="random">The random provider to use.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>The created random scalar instance.</returns>
        static abstract TElementType GetRandomScalar<TRandom>(
            ref TRandom random,
            TElementType min,
            TElementType max)
            where TRandom : struct, IRandomProvider;
        
        /// <summary>
        /// Creates a random vector instance falling into the range of min max.
        /// </summary>
        /// <param name="random">The random provider to use.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>The created random vector instance.</returns>
        static abstract TSelf GetRandom<TRandom>(
            ref TRandom random,
            TSelf min,
            TSelf max)
            where TRandom : struct, IRandomProvider;

        /// <summary>
        /// Creates a vector instance from the given source view.
        /// </summary>
        /// <typeparam name="TStride">The custom stride type.</typeparam>
        /// <param name="sourceView">The source view.</param>
        /// <param name="index">The source base index.</param>
        /// <returns>The vector instance.</returns>
        static abstract TSelf FromElementView<TStride>(
            ArrayView1D<TElementType, TStride> sourceView,
            Index1D index)
            where TStride : struct, IStride1D;

        /// <summary>
        /// Serializes the current vector instance into the given target view.
        /// </summary>
        /// <typeparam name="TStride">The custom stride type.</typeparam>
        /// <param name="targetView">The target view.</param>
        /// <param name="index">The target base index.</param>
        void ToElementView<TStride>(
            ArrayView1D<TElementType, TStride> targetView,
            Index1D index)
            where TStride : struct, IStride1D;

        /// <summary>
        /// Creates a vector instance from the given source view.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="index">The source base index.</param>
        /// <returns>The vector instance.</returns>
        static abstract TSelf FromElementView(
            SingleVectorView<TElementType> sourceView,
            Index1D index);

        /// <summary>
        /// Serializes the current vector instance into the given target view.
        /// </summary>
        /// <param name="targetView">The target view.</param>
        /// <param name="index">The target base index.</param>
        void ToElementView(
            SingleVectorView<TElementType> targetView,
            Index1D index);

        /// <summary>
        /// Converts a scalar value into the current vectorized type.
        /// </summary>
        /// <param name="scalar">The scalar element type.</param>
        /// <returns>The created vectorized type.</returns>
        static abstract TSelf FromScalar(TElementType scalar);

        /// <summary>
        /// Converts this instance into an unsafe span instance.
        /// </summary>
        /// <returns>The readonly span instance.</returns>
        ReadOnlySpan<TElementType> AsSpan();

        /// <summary>
        /// Iterates over all elements by applying the given iterator to each element.
        /// </summary>
        /// <typeparam name="TIterator">The managed iterator type.</typeparam>
        /// <param name="iterator">The iterator to invoke.</param>
        void ForEach<TIterator>(ref TIterator iterator)
            where TIterator : struct, IVectorElementIterator<TSelf, TElementType>;
    }
    
    /// <summary>
    /// An abstract numeric accumulation vector type with a number of elements.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type.</typeparam>
    public interface IAccumulationVectorType<TSelf> : IVectorType<TSelf>
        where TSelf : unmanaged, IAccumulationVectorType<TSelf>
    {
        /// <summary>
        /// Atomically adds two vectors.
        /// </summary>
        /// <param name="target">The target memory address.</param>
        /// <param name="value">The current value to add.</param>
        static abstract void AtomicAdd(ref TSelf target, TSelf value);
    }

    /// <summary>
    /// An abstract numeric vector type with a number of elements.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type.</typeparam>
    /// <typeparam name="TOther">The type to accumulate.</typeparam>
    /// <typeparam name="TOtherElementType">
    /// The underlying element type of the other type.
    /// </typeparam>
    public interface IAccumulationVectorType<TSelf, TOther, TOtherElementType>
        : IAccumulationVectorType<TSelf>
        where TSelf : unmanaged, IAccumulationVectorType<TSelf, TOther, TOtherElementType>
        where TOther : unmanaged, IVectorType<TOther, TOtherElementType>
        where TOtherElementType : unmanaged, INumber<TOtherElementType>
    {
        /// <summary>
        /// Adds an accumulation instance and a more coarse grained value instance.
        /// </summary>
        /// <param name="toAccumulate">The precise accumulation instance.</param>
        /// <param name="current">The value to add.</param>
        /// <returns>The accumulated instance.</returns>
        static abstract TSelf operator +(TSelf current, TOther toAccumulate);
        
        /// <summary>
        /// Adds an accumulation instance and a more coarse grained value instance.
        /// </summary>
        /// <param name="toAccumulate">The precise accumulation instance.</param>
        /// <param name="current">The value to add.</param>
        /// <returns>The accumulated instance.</returns>
        static abstract TSelf operator +(TOther toAccumulate, TSelf current);
        
        /// <summary>
        /// Subtracts an accumulation instance and a more coarse grained value instance.
        /// </summary>
        /// <param name="toAccumulate">The precise accumulation instance.</param>
        /// <param name="current">The value to subtract.</param>
        /// <returns>The accumulated instance.</returns>
        static abstract TSelf operator -(TSelf current, TOther toAccumulate);
        
        /// <summary>
        /// Subtracts an accumulation instance and a more coarse grained value instance.
        /// </summary>
        /// <param name="toAccumulate">The precise accumulation instance.</param>
        /// <param name="current">The value to subtract.</param>
        /// <returns>The accumulated instance.</returns>
        static abstract TSelf operator -(TOther toAccumulate, TSelf current);

        /// <summary>
        /// Computes the average while using the provided denominator.
        /// </summary>
        /// <param name="denominator">The denominator to use.</param>
        /// <returns>The computed average.</returns>
        TSelf ComputeAverage(long denominator);
        
        /// <summary>
        /// Computes the average while using the provided denominator.
        /// </summary>
        /// <param name="denominator">The denominator to use.</param>
        /// <returns>The computed average.</returns>
        TSelf ComputeAverage(TOtherElementType denominator);
        
        /// <summary>
        /// Atomically adds two vectors.
        /// </summary>
        /// <param name="target">The target memory address.</param>
        /// <param name="value">The current value to add.</param>
        static abstract void AtomicAdd(ref TSelf target, TOther value);

        /// <summary>
        /// Converts a given coarse-grained value instance into its corresponding
        /// accumulation value.
        /// </summary>
        /// <param name="other">The value to convert.</param>
        /// <returns>The converted value.</returns>
        static abstract TSelf ConvertFromBase(TOther other);
        
        /// <summary>
        /// Converts a given fine-grained value instance into its corresponding
        /// raw value.
        /// </summary>
        /// <param name="current">The value to convert.</param>
        /// <returns>The converted value.</returns>
        static abstract TOther ConvertToBase(TSelf current);
    }
}

#pragma warning restore CA2225
#pragma warning restore CA1000

#endif
