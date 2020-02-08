// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelConfig.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using System;
using System.Diagnostics;
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
    public readonly struct KernelConfig : IIndex
    {
        #region Static

        /// <summary>
        /// Represents a kernel constructor for implicitly grouped kernels.
        /// </summary>
        internal static readonly ConstructorInfo ImplicitlyGroupedKernelConstructor =
            typeof(KernelConfig).GetConstructor(new Type[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int)
                });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index gridDimension, Index groupDimension)
            : this(gridDimension, groupDimension, default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">The dynamic shared memory configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index gridDimension,
            Index groupDimension,
            DynamicSharedMemoryConfig sharedMemoryConfig)
            : this(
                  new Index3(gridDimension.X, 1, 1),
                  new Index3(groupDimension.X, 1, 1),
                  sharedMemoryConfig)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index2 gridDimension, Index2 groupDimension)
            : this(gridDimension, groupDimension, default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">The dynamic shared memory configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index2 gridDimension,
            Index2 groupDimension,
            DynamicSharedMemoryConfig sharedMemoryConfig)
            : this(
                  new Index3(gridDimension.X, gridDimension.Y, 1),
                  new Index3(groupDimension.X, groupDimension.Y, 1),
                  sharedMemoryConfig)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index3 gridDimension, Index3 groupDimension)
            : this(gridDimension, groupDimension, default)
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
                  new Index3(gridDimX, gridDimY, gridDimZ),
                  new Index3(groupDimX, groupDimY, groupDimZ),
                  default)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemoryConfig">The dynamic shared memory configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index3 gridDimension,
            Index3 groupDimension,
            DynamicSharedMemoryConfig sharedMemoryConfig)
        {
            Debug.Assert(gridDimension.Size >= 0, "Invalid grid dimension");
            Debug.Assert(groupDimension.Size >= 0, "Invalid group dimension");
            Debug.Assert(sharedMemoryConfig.IsValid, "Invalid shared memory configuration");

            GridDimension = gridDimension;
            GroupDimension = groupDimension;
            SharedMemoryConfig = sharedMemoryConfig;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the global grid dimension.
        /// </summary>
        public Index3 GridDimension { get; }

        /// <summary>
        /// Returns the global group dimension of each group.
        /// </summary>
        public Index3 GroupDimension { get; }

        /// <summary>
        /// Returns the associated dynamic memory configuration.
        /// </summary>
        public DynamicSharedMemoryConfig SharedMemoryConfig { get; }

        /// <summary>
        /// Returns true if the current configuration uses dynamic shared memory.
        /// </summary>
        public bool UsesDynamicSharedMemory => SharedMemoryConfig.NumElements > 0;

        /// <summary>
        /// Returns true if this configuration is a valid launch configuration.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (GridDimension.X > 0 & GridDimension.Y > 0 & GridDimension.Z > 0) &&
                    (GroupDimension.X > 0 & GroupDimension.Y > 0 & GroupDimension.Z > 0) &&
                    SharedMemoryConfig.IsValid;
            }
        }

        /// <summary>
        /// Returns the total launch size.
        /// </summary>
        public int Size => GridDimension.Size * GroupDimension.Size;

        #endregion

        #region Methods

        /// <summary>
        /// Converts the current instance into a dimension tuple.
        /// </summary>
        /// <returns>A dimension tuple representing this kernel configuration.</returns>
        public (Index3, Index3) ToDimensions() => (GridDimension, GroupDimension);

        /// <summary>
        /// Converts the current instance into a value tuple.
        /// </summary>
        /// <returns>A value tuple representing this kernel configuration.</returns>
        public (Index3, Index3, DynamicSharedMemoryConfig) ToValueTuple() =>
            (GridDimension, GroupDimension, SharedMemoryConfig);

        /// <summary>
        /// Deconstructs the current instance into a dimension tuple.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Index3 gridDimension, out Index3 groupDimension)
        {
            gridDimension = GridDimension;
            groupDimension = GroupDimension;
        }

        /// <summary>
        /// Deconstructs the current instance into a value tuple.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        /// <param name="sharedMemoryConfig">The shared memory configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Index3 gridDimension,
            out Index3 groupDimension,
            out DynamicSharedMemoryConfig sharedMemoryConfig)
        {
            gridDimension = GridDimension;
            groupDimension = GroupDimension;
            sharedMemoryConfig = SharedMemoryConfig;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3(dimensions.Item2, 1, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index2) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3(dimensions.Item2, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index3) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2);

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig(
            (Index3, Index3, DynamicSharedMemoryConfig) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2, dimensions.Item3);

        /// <summary>
        /// Converts the given kernel configuration into an equivalent dimension tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator (Index3, Index3)(KernelConfig config) =>
            config.ToDimensions();

        /// <summary>
        /// Converts the given kernel configuration into an equivalent value tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator (Index3, Index3, DynamicSharedMemoryConfig)(KernelConfig config) =>
            config.ToValueTuple();

        #endregion
    }

    /// <summary>
    /// Represents a dynamic shared memory configuration for kernels.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DynamicSharedMemoryConfig
    {
        #region Instance

        /// <summary>
        /// Constructs a new shared memory configuration.
        /// </summary>
        /// <param name="numElements">The number of elements to allocate.</param>
        public DynamicSharedMemoryConfig(int numElements)
        {
            Debug.Assert(numElements >= 0, "Invalid number of shared-memory elements");
            NumElements = numElements;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public int NumElements { get; }

        /// <summary>
        /// Returns true if this is a valid configuration.
        /// </summary>
        public bool IsValid => NumElements >= 0;

        #endregion
    }

    /// <summary>
    /// A shared memory configuration that stores both static and dynamic information about
    /// shared memory.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SharedMemoryConfig
    {
        /// <summary>
        /// Constructs a new shared memory configuration.
        /// </summary>
        /// <param name="specification">The general specification.</param>
        /// <param name="dynamicConfig">The dynamic configuration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SharedMemoryConfig(
            SharedMemorySpecification specification,
            DynamicSharedMemoryConfig dynamicConfig)
        {
            Specification = specification;
            DynamicConfig = dynamicConfig;
        }

        /// <summary>
        /// Returns the static specification.
        /// </summary>
        public SharedMemorySpecification Specification { get; }

        /// <summary>
        /// Returns the dynamic configuration.
        /// </summary>
        public DynamicSharedMemoryConfig DynamicConfig { get; }

        /// <summary>
        /// Returns the number of dynamic shared memory elements.
        /// </summary>
        public int NumDynamicElements => DynamicConfig.NumElements;

        /// <summary>
        /// Returns the array size of the dynamically allocated shared memory <inheritdocbytes./>
        /// </summary>
        public int DynamicArraySize => NumDynamicElements * DynamicElementSize;

        /// <summary>
        /// Returns true if the current specification.
        /// </summary>
        public bool HasSharedMemory => Specification.HasSharedMemory;

        /// <summary>
        /// Returns the amount of shared memory.
        /// </summary>
        public int StaticSize => Specification.StaticSize;

        /// <summary>
        /// Returns true if the current specification required static shared memory.
        /// </summary>
        public bool HasStaticMemory => Specification.HasStaticMemory;

        /// <summary>
        /// Returns the element size of a dynamic shared memory element (if any).
        /// </summary>
        public int DynamicElementSize => Specification.DynamicElementSize;

        /// <summary>
        /// Returns true if this entry point required dynamic shared memory.
        /// </summary>
        public bool HasDynamicMemory => Specification.HasDynamicMemory;
    }

}
