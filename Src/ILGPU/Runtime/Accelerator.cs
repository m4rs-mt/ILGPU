// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Accelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the general type of an accelerator.
    /// </summary>
    public enum AcceleratorType
    {
        /// <summary>
        /// Represents a CPU accelerator.
        /// </summary>
        CPU,

        /// <summary>
        /// Represents a Cuda accelerator.
        /// </summary>
        Cuda
    }

    /// <summary>
    /// Represents a general abstract accelerator.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class Accelerator : DisposeBase
    {
        #region Static

        /// <summary>
        /// Represents the list of available accelerators.
        /// </summary>
        private static List<AcceleratorId> accelerators;

        /// <summary>
        /// Returns a list of available accelerators.
        /// </summary>
        public static IReadOnlyList<AcceleratorId> Accelerators
        {
            get
            {
                if (accelerators == null)
                {
                    accelerators = new List<AcceleratorId>(4);
                    accelerators.AddRange(Cuda.CudaAccelerator.CudaAccelerators);
                    accelerators.Add(new AcceleratorId(AcceleratorType.CPU, 0));
                }
                return accelerators;
            }
        }

        /// <summary>
        /// Creates the specified accelerator using the provided accelerator id.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The specified accelerator id.</param>
        /// <returns>The created accelerator.</returns>
        public static Accelerator Create(Context context, AcceleratorId acceleratorId)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            switch (acceleratorId.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    return new CPU.CPUAccelerator(context);
                case AcceleratorType.Cuda:
                    return new Cuda.CudaAccelerator(context, acceleratorId.DeviceId);
                default:
                    throw new ArgumentException(RuntimeErrorMessages.NotSupportedTargetAccelerator, nameof(acceleratorId));
            }
        }

        /// <summary>
        /// Tries to resolve the accelerator type of the given pointer.
        /// </summary>
        /// <param name="value">The pointer to check.</param>
        /// <returns>A value of the <see cref="AcceleratorType"/> enum iff the type could be rsesolved.</returns>
        public static AcceleratorType? TryResolvePointerType(IntPtr value)
        {
            // Try to copy data from the associated accelerator
            try
            {
                // Try to detect whether this pointer is a cuda pointer...
                var cudaMemoryType = Cuda.CudaAccelerator.GetCudaMemoryType(value);
                if (cudaMemoryType != Cuda.CudaMemoryType.None && cudaMemoryType != Cuda.CudaMemoryType.Host)
                    return AcceleratorType.Cuda;
            }
            catch (Exception e) when (
                e is DllNotFoundException || // The driver api could not be found
                e is EntryPointNotFoundException || // The driver api does not support this feature
                e is TypeInitializationException || // The general accelerator class could not be initialized
                e is Cuda.CudaException) // An internal cuda exception was thrown
            {
                // We cannot resolve any information from cuda...
            }
            return AcceleratorType.CPU;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Contains a collection of all peer accelerators.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly HashSet<Accelerator> storedPeerAccelerators = new HashSet<Accelerator>();

        /// <summary>
        /// Constructs a new accelerator.
        /// </summary>
        /// <param name="context">The target context.</param>
        /// <param name="type">The target accelerator type.</param>
        internal Accelerator(Context context, AcceleratorType type)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            AcceleratorType = type;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated ILGPU context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the default stream of this accelerator.
        /// </summary>
        public AcceleratorStream DefaultStream { get; protected set; }

        /// <summary>
        /// Returns the type of the accelerator.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

        /// <summary>
        /// Returns the memory size in bytes.
        /// </summary>
        public long MemorySize { get; protected set; }

        /// <summary>
        /// Returns the accelerators for which the peer access has been enabled.
        /// </summary>
        public IReadOnlyCollection<Accelerator> PeerAccelerators => storedPeerAccelerators;

        /// <summary>
        /// Returns the cached peer accesses.
        /// </summary>
        protected HashSet<Accelerator> CachedPeerAccelerators => storedPeerAccelerators;

        /// <summary>
        /// Returns the name of the device.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Returns the max grid size.
        /// </summary>
        public Index3 MaxGridSize { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        public int MaxThreadsPerGroup { get; protected set; }

        /// <summary>
        /// Returns the maximum number of shared memory per thread group in bytes.
        /// </summary>
        public int MaxSharedMemoryPerGroup { get; protected set; }

        /// <summary>
        /// Returns the maximum number of constant memory in bytes.
        /// </summary>
        public int MaxConstantMemory { get; protected set; }

        /// <summary>
        /// Return the warp size.
        /// </summary>
        public int WarpSize { get; protected set; }

        /// <summary>
        /// Returns the number of available multiprocessors.
        /// </summary>
        public int NumMultiprocessors { get; protected set; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new accelerator extension using the given provider.
        /// </summary>
        /// <typeparam name="TExtension">The type of the extension to create.</typeparam>
        /// <typeparam name="TExtensionProvider">The extension provided type to create the extension.</typeparam>
        /// <param name="provider">The extension provided to create the extension.</param>
        /// <returns>The created extension.</returns>
        public abstract TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider)
            where TExtensionProvider : IAcceleratorExtensionProvider<TExtension>;

        /// <summary>
        /// Creates a new backend that is compatible with this accelerator.
        /// </summary>
        /// <returns>The created compatible backend.</returns>
        public abstract Backend CreateBackend();

        /// <summary>
        /// Allocates a buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        public abstract MemoryBuffer<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : struct
            where TIndex : struct, IIndex, IGenericIndex<TIndex>;

        /// <summary>
        /// Allocates a 1D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 1D buffer on the this accelerator.</returns>
        public MemoryBuffer<T> Allocate<T>(int extent)
            where T : struct
        {
            return new MemoryBuffer<T>(Allocate<T, Index>(extent));
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer2D<T> Allocate<T>(Index2 extent)
            where T : struct
        {
            return new MemoryBuffer2D<T>(Allocate<T, Index2>(extent));
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 2D buffer.</param>
        /// <param name="height">The height of the 2D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer2D<T> Allocate<T>(int width, int height)
            where T : struct
        {
            return Allocate<T>(new Index2(width, height));
        }

        /// <summary>
        /// Allocates a 3D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        public MemoryBuffer3D<T> Allocate<T>(Index3 extent)
            where T : struct
        {
            return new MemoryBuffer3D<T>(Allocate<T, Index3>(extent));
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 3D buffer.</param>
        /// <param name="height">The height of the 3D buffer.</param>
        /// <param name="depth">The depth of the 3D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer3D<T> Allocate<T>(int width, int height, int depth)
            where T : struct
        {
            return Allocate<T>(new Index3(width, height, depth));
        }

        /// <summary>
        /// Loads the given kernel.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <returns>The loaded kernel.</returns>
        public abstract Kernel LoadKernel(CompiledKernel kernel);

        /// <summary>
        /// Loads the given implicitly-grouped kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public abstract Kernel LoadImplicitlyGroupedKernel(
            CompiledKernel kernel,
            int customGroupSize);

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <returns>The loaded kernel.</returns>
        public Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel)
        {
            return LoadAutoGroupedKernel(kernel, out int groupSize, out int minGridSize);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel.</returns>
        public abstract Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize);

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <returns>The created accelerator stream.</returns>
        public abstract AcceleratorStream CreateStream();

        /// <summary>
        /// Synchronizes pending operations.
        /// </summary>
        public abstract void Synchronize();

        /// <summary>
        /// Makes this accelerator the current one for this thread.
        /// </summary>
        public abstract void MakeCurrent();

        #endregion

        #region Peer Access

        /// <summary>
        /// Returns true iff peer access between the current and the given accelerator has been enabled.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPeerAccess(Accelerator accelerator)
        {
            return storedPeerAccelerators.Contains(accelerator);
        }

        /// <summary>
        /// Returns true iff the current accelerator can directly access the memory
        /// of the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, iff the current accelerator can directly access the memory
        /// of the given accelerator.</returns>
        public abstract bool CanAccessPeer(Accelerator otherAccelerator);

        /// <summary>
        /// Enables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        public abstract void EnablePeerAccess(Accelerator otherAccelerator);

        /// <summary>
        /// Disables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        public abstract void DisablePeerAccess(Accelerator otherAccelerator);

        #endregion

        #region Occupancy

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a single multiprocessor.
        /// </summary>
        /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupDim">The group dimension.</param>
        /// <returns>The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.</returns>
        public float EstimateOccupancyPerMultiprocessor<TIndex>(Kernel kernel, TIndex groupDim)
            where TIndex : IIndex
        {
            return EstimateOccupancyPerMultiprocessor(kernel, groupDim.Size);
        }

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a single multiprocessor.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <returns>The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.</returns>
        public float EstimateOccupancyPerMultiprocessor(Kernel kernel, int groupSize)
        {
            return EstimateOccupancyPerMultiprocessor(kernel, groupSize, 0);
        }

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a single multiprocessor.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <returns>The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.</returns>
        public float EstimateOccupancyPerMultiprocessor(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            var maxActiveGroups = EstimateMaxActiveGroupsPerMultiprocessor(
                kernel,
                groupSize,
                dynamicSharedMemorySizeInBytes);
            return (maxActiveGroups * groupSize) / (float)MaxThreadsPerGroup;
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the given kernel.
        /// </summary>
        /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
        /// <param name="kernel">The kernel used for the computation of the maximum number of active groups.</param>
        /// <param name="groupDim">The group dimension.</param>
        /// <returns>The maximum number of active groups per multiprocessor for the given kernel.</returns>
        public int EstimateMaxActiveGroupsPerMultiprocessor<TIndex>(Kernel kernel, TIndex groupDim)
            where TIndex : IIndex
        {
            return EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupDim.Size);
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <returns>The maximum number of active groups per multiprocessor for the given kernel.</returns>
        public int EstimateMaxActiveGroupsPerMultiprocessor(Kernel kernel, int groupSize)
        {
            return EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupSize, 0);
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <returns>The maximum number of active groups per multiprocessor for the given kernel.</returns>
        public int EstimateMaxActiveGroupsPerMultiprocessor(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (groupSize < 1)
                throw new ArgumentNullException(nameof(groupSize));
            if (dynamicSharedMemorySizeInBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicSharedMemorySizeInBytes));
            return EstimateMaxActiveGroupsPerMultiprocessorInternal(
                kernel,
                groupSize,
                dynamicSharedMemorySizeInBytes);
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <remarks>Note that the arguments do not have to be verified since they are already verified.</remarks>
        /// <returns>The maximum number of active groups per multiprocessor for the given kernel.</returns>
        protected abstract int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(Kernel kernel)
        {
            return EstimateGroupSize(kernel, 0, 0, out int minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(Kernel kernel, out int minGridSize)
        {
            return EstimateGroupSize(kernel, 0, 0, out minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            out int minGridSize)
        {
            return EstimateGroupSize(
                kernel,
                dynamicSharedMemorySizeInBytes,
                0,
                out minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="computeSharedMemorySize">A callback to compute the required amount of shared memory in bytes for a given group size.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            out int minGridSize)
        {
            return EstimateGroupSize(
                kernel,
                computeSharedMemorySize,
                0,
                out minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="computeSharedMemorySize">A callback to compute the required amount of shared memory in bytes for a given group size.</param>
        /// <param name="maxGroupSize">The maximum group-size limit on a single multiprocessor.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (computeSharedMemorySize == null)
                throw new ArgumentNullException(nameof(computeSharedMemorySize));
            if (maxGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxGroupSize));
            return EstimateGroupSizeInternal(
                kernel,
                computeSharedMemorySize,
                maxGroupSize,
                out minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="computeSharedMemorySize">A callback to compute the required amount of shared memory in bytes for a given group size.</param>
        /// <param name="maxGroupSize">The maximum group-size limit on a single multiprocessor.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <remarks>Note that the arguments do not have to be verified since they are already verified.</remarks>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        protected abstract int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <param name="maxGroupSize">The maximum group-size limit on a single multiprocessor.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        public int EstimateGroupSize(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (dynamicSharedMemorySizeInBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicSharedMemorySizeInBytes));
            if (maxGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxGroupSize));
            return EstimateGroupSizeInternal(
                kernel,
                dynamicSharedMemorySizeInBytes,
                maxGroupSize,
                out minGridSize);
        }

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">The required dynamic shared-memory size in bytes.</param>
        /// <param name="maxGroupSize">The maximum group-size limit on a single multiprocessor.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <remarks>Note that the arguments do not have to be verified since they are already verified.</remarks>
        /// <returns>An estimated group size to gain maximum occupancy on this device.</returns>
        protected abstract int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize);

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this accelerator.
        /// </summary>
        /// <returns>The string representation of this accelerator.</returns>
        public override string ToString()
        {
            return $"{Name} [WarpSize: {WarpSize}, MaxThreadsPerGroup: {MaxThreadsPerGroup}, MemorySize: {MemorySize}]";
        }

        #endregion
    }
}
