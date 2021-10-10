﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelConfig.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Resources;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// A single kernel configuration for an explicitly grouped kernel.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    [IndexType(IndexType.KernelConfig)]
    public readonly struct KernelConfig : IIntrinsicIndex
    {
        #region Static

        /// <summary>
        /// Represents a kernel constructor for implicitly grouped kernels.
        /// </summary>
        internal static readonly ConstructorInfo ImplicitlyGroupedKernelConstructor =
            typeof(KernelConfig).GetConstructor(new Type[]
                {
                    typeof(Index3D),
                    typeof(Index3D)
                });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index1D gridDim, Index1D groupDim)
            : this(gridDim, groupDim, default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">
        /// The dynamic shared memory configuration.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index1D gridDim,
            Index1D groupDim,
            SharedMemoryConfig sharedMemoryConfig)
            : this(
                  new Index3D(gridDim.X, 1, 1),
                  new Index3D(groupDim.X, 1, 1),
                  sharedMemoryConfig)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index2D gridDim, Index2D groupDim)
            : this(gridDim, groupDim, default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">
        /// The dynamic shared memory configuration.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index2D gridDim,
            Index2D groupDim,
            SharedMemoryConfig sharedMemoryConfig)
            : this(
                  new Index3D(gridDim.X, gridDim.Y, 1),
                  new Index3D(groupDim.X, groupDim.Y, 1),
                  sharedMemoryConfig)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index3D gridDim, Index3D groupDim)
            : this(gridDim, groupDim, default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimX">The grid dimension in X dimension.</param>
        /// <param name="gridDimY">The grid dimension in Y dimension.</param>
        /// <param name="gridDimZ">The grid dimension in Z dimension.</param>
        /// <param name="groupDimX">The group dimension in X dimension.</param>
        /// <param name="groupDimY">The group dimension in Y dimension.</param>
        /// <param name="groupDimZ">The group dimension in Z dimension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            int gridDimX, int gridDimY, int gridDimZ,
            int groupDimX, int groupDimY, int groupDimZ)
            : this(
                  new Index3D(gridDimX, gridDimY, gridDimZ),
                  new Index3D(groupDimX, groupDimY, groupDimZ),
                  default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDim">The grid dimension to use.</param>
        /// <param name="groupDim">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">
        /// The dynamic shared memory configuration.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index3D gridDim,
            Index3D groupDim,
            SharedMemoryConfig sharedMemoryConfig)
        {
            if (gridDim.LongSize < 0)
                throw new ArgumentOutOfRangeException(nameof(gridDim));
            if (groupDim.LongSize < 0)
                throw new ArgumentOutOfRangeException(nameof(groupDim));

            GridDim = gridDim;
            GroupDim = groupDim;
            SharedMemoryConfig = sharedMemoryConfig;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public readonly IndexType IndexType => IndexType.KernelConfig;

        /// <summary>
        /// Returns the global grid dimension.
        /// </summary>
        public Index3D GridDim { get; }

        /// <summary>
        /// Returns the global group dimension of each group.
        /// </summary>
        public Index3D GroupDim { get; }

        /// <summary>
        /// Returns the associated dynamic memory configuration.
        /// </summary>
        public SharedMemoryConfig SharedMemoryConfig { get; }

        /// <summary>
        /// Returns true if the current configuration uses dynamic shared memory.
        /// </summary>
        public readonly bool UsesDynamicSharedMemory =>
            SharedMemoryConfig.UsesDynamicSharedMemory;

        /// <summary>
        /// Returns true if this configuration is a valid launch configuration.
        /// </summary>
        public readonly bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                GridDim.X > 0 & GridDim.Y > 0 & GridDim.Z > 0 &&
                GroupDim.X > 0 & GroupDim.Y > 0 & GroupDim.Z > 0;
        }

        /// <summary>
        /// Returns the total launch size.
        /// </summary>
        public readonly long Size => GridDim.LongSize * GroupDim.LongSize;

        #endregion

        #region Methods

        /// <summary>
        /// Converts the current instance into a dimension tuple.
        /// </summary>
        /// <returns>A dimension tuple representing this kernel configuration.</returns>
        public readonly (Index3D, Index3D) ToDimensions() => (GridDim, GroupDim);

        /// <summary>
        /// Converts the current instance into a value tuple.
        /// </summary>
        /// <returns>A value tuple representing this kernel configuration.</returns>
        public readonly (Index3D, Index3D, SharedMemoryConfig) ToValueTuple() =>
            (GridDim, GroupDim, SharedMemoryConfig);

        /// <summary>
        /// Deconstructs the current instance into a dimension tuple.
        /// </summary>
        /// <param name="gridDim">The grid dimension.</param>
        /// <param name="groupDim">The group dimension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Index3D gridDim, out Index3D groupDim)
        {
            gridDim = GridDim;
            groupDim = GroupDim;
        }

        /// <summary>
        /// Deconstructs the current instance into a value tuple.
        /// </summary>
        /// <param name="gridDim">The grid dimension.</param>
        /// <param name="groupDim">The group dimension.</param>
        /// <param name="sharedMemoryConfig">The shared memory configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(
            out Index3D gridDim,
            out Index3D groupDim,
            out SharedMemoryConfig sharedMemoryConfig)
        {
            gridDim = GridDim;
            groupDim = GroupDim;
            sharedMemoryConfig = SharedMemoryConfig;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index1D, Index1D) dimensions) =>
            new KernelConfig(
                new Index3D(dimensions.Item1, 1, 1),
                new Index3D(dimensions.Item2, 1, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3D, Index1D) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3D(dimensions.Item2, 1, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3D, Index2D) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3D(dimensions.Item2, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index2D, Index2D) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2);

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3D, Index3D) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2);

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig(
            (Index1D, Index1D, SharedMemoryConfig) dimensions) =>
            new KernelConfig(
                new Index3D(dimensions.Item1, 1, 1),
                new Index3D(dimensions.Item2, 1, 1),
                dimensions.Item3);

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig(
            (Index3D, Index3D, SharedMemoryConfig) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2, dimensions.Item3);

        /// <summary>
        /// Converts the given kernel configuration into an equivalent dimension tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator (Index3D, Index3D)(KernelConfig config) =>
            config.ToDimensions();

        /// <summary>
        /// Converts the given kernel configuration into an equivalent value tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator
            (Index3D, Index3D, SharedMemoryConfig)(KernelConfig config) =>
            config.ToValueTuple();

        #endregion
    }

    /// <summary>
    /// Represents a dynamic shared memory configuration for kernels.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SharedMemoryConfig
    {
        #region Static

        /// <summary>
        /// An empty shared memory configuration.
        /// </summary>
        /// <remarks>
        /// This configuration does not use dynamic shared memory.
        /// </remarks>
        public static readonly SharedMemoryConfig Empty;

        /// <summary>
        /// Requests a <see cref="SharedMemoryConfig"/>
        /// </summary>
        /// <typeparam name="T">The element type to use.</typeparam>
        /// <param name="numElements">The number of elements to request.</param>
        /// <returns>A shared memory configuration that uses shared memory.</returns>
        public static SharedMemoryConfig RequestDynamic<T>(int numElements)
            where T : unmanaged
        {
            if (numElements < 1)
                throw new ArgumentOutOfRangeException(nameof(numElements));
            var elementSize = Interop.SizeOf<T>();
            return new SharedMemoryConfig(numElements, elementSize);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new shared memory configuration.
        /// </summary>
        /// <param name="numElements">The number of elements to allocate.</param>
        /// <param name="elementSize">The element size to allocate.</param>
        public SharedMemoryConfig(int numElements, int elementSize)
        {
            if (numElements < 0)
                throw new ArgumentOutOfRangeException(nameof(numElements));
            if (elementSize < 0)
                throw new ArgumentOutOfRangeException(nameof(elementSize));

            NumElements = numElements;
            ElementSize = elementSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public int NumElements { get; }

        /// <summary>
        /// Returns the element size in bytes.
        /// </summary>
        public int ElementSize { get; }

        /// <summary>
        /// Returns the array size in bytes of the dynamically allocated shared memory.
        /// </summary>
        public int ArraySize => NumElements * ElementSize;

        /// <summary>
        /// Returns true if this configuration uses dynamic shared memory.
        /// </summary>
        public bool UsesDynamicSharedMemory => NumElements > 0 & ElementSize > 0;

        #endregion
    }

    /// <summary>
    /// A shared memory configuration that stores both static and dynamic information
    /// about shared memory.
    /// </summary>
    [Serializable]
    public readonly struct RuntimeSharedMemoryConfig
    {
        /// <summary>
        /// Constructs a new shared memory configuration.
        /// </summary>
        /// <param name="specification">The general specification.</param>
        /// <param name="dynamicConfig">The dynamic configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuntimeSharedMemoryConfig(
            SharedMemorySpecification specification,
            SharedMemoryConfig dynamicConfig)
        {
            Specification = specification;
            DynamicConfig = dynamicConfig;

            if (!specification.HasDynamicMemory && dynamicConfig.NumElements > 0)
            {
                throw new InvalidOperationException(
                    ErrorMessages.InvalidDynamicSharedMemoryConfiguration);
            }
        }

        /// <summary>
        /// Returns the static specification.
        /// </summary>
        public SharedMemorySpecification Specification { get; }

        /// <summary>
        /// Returns the dynamic configuration.
        /// </summary>
        public SharedMemoryConfig DynamicConfig { get; }

        /// <summary>
        /// Returns the number of dynamic shared memory elements.
        /// </summary>
        public int NumDynamicElements => DynamicConfig.NumElements;

        /// <summary>
        /// Returns the array size in bytes of the dynamically allocated shared memory.
        /// </summary>
        public int DynamicArraySize => DynamicConfig.ArraySize;

        /// <summary>
        /// Returns true if the current specification.
        /// </summary>
        public bool HasSharedMemory => Specification.HasSharedMemory;

        /// <summary>
        /// Returns the amount of shared memory.
        /// </summary>
        public int StaticSize => Specification.StaticSize;

        /// <summary>
        /// Returns true if the current config requires static shared memory.
        /// </summary>
        public bool HasStaticMemory => Specification.HasStaticMemory;

        /// <summary>
        /// Returns true if the current config requires dynamic shared memory.
        /// </summary>
        public bool HasDynamicMemory => Specification.HasDynamicMemory;
    }

    /// <summary>
    /// Represents a runtime kernel configuration that is used internally to specify
    /// launch dimensions and shared memory settings.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RuntimeKernelConfig
    {
        #region Static

        /// <summary>
        /// Represents the associated constructor.
        /// </summary>
        internal static ConstructorInfo Constructor = typeof(RuntimeKernelConfig).
            GetConstructor(new Type[]
            {
                typeof(KernelConfig),
                typeof(SharedMemorySpecification)
            });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new runtime kernel configuration.
        /// </summary>
        /// <param name="kernelConfig">The kernel configuration to use.</param>
        /// <param name="specification">The shared memory specification to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuntimeKernelConfig(
            KernelConfig kernelConfig,
            SharedMemorySpecification specification)
        {
            GridDim = kernelConfig.GridDim;
            GroupDim = kernelConfig.GroupDim;
            SharedMemoryConfig = new RuntimeSharedMemoryConfig(
                specification,
                kernelConfig.SharedMemoryConfig);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the global grid dimension.
        /// </summary>
        public Index3D GridDim { get; }

        /// <summary>
        /// Returns the global group dimension of each group.
        /// </summary>
        public Index3D GroupDim { get; }

        /// <summary>
        /// Returns the current shared memory configuration.
        /// </summary>
        public RuntimeSharedMemoryConfig SharedMemoryConfig { get; }

        /// <summary>
        /// Returns true if this configuration is a valid launch configuration.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                GridDim.X > 0 & GridDim.Y > 0 & GridDim.Z > 0 &&
                GroupDim.X > 0 & GroupDim.Y > 0 & GroupDim.Z > 0;
        }

        #endregion
    }
}
