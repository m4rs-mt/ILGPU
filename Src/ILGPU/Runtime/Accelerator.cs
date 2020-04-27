// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Accelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Frontend.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the general type of an accelerator.
    /// </summary>
    public enum AcceleratorType : int
    {
        /// <summary>
        /// Represents a CPU accelerator.
        /// </summary>
        CPU,

        /// <summary>
        /// Represents a Cuda accelerator.
        /// </summary>
        Cuda,

        /// <summary>
        /// Represents an OpenCL accelerator (CPU/GPU via OpenCL).
        /// </summary>
        OpenCL,
    }

    /// <summary>
    /// Represents an abstract accelerator extension that can store additional data.
    /// </summary>
    public abstract class AcceleratorExtension : CachedExtension { }

    /// <summary>
    /// Represents a general abstract accelerator.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract partial class Accelerator :
        CachedExtensionBase<AcceleratorExtension>
    {
        #region Static

        /// <summary>
        /// Detects all accelerators.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Complex initialization logic is required in this case")]
        static Accelerator()
        {
            var accelerators = ImmutableArray.CreateBuilder<AcceleratorId>(4);
            accelerators.AddRange(CPU.CPUAccelerator.CPUAccelerators);
            accelerators.AddRange(Cuda.CudaAccelerator.CudaAccelerators);
            accelerators.AddRange(OpenCL.CLAccelerator.CLAccelerators);
            Accelerators = accelerators.ToImmutable();
        }

        /// <summary>
        /// Represents all available accelerators.
        /// </summary>
        public static ImmutableArray<AcceleratorId> Accelerators { get; }

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

            switch (acceleratorId)
            {
                case CPU.CPUAcceleratorId _:
                    return new CPU.CPUAccelerator(context);
                case Cuda.CudaAcceleratorId cudaId:
                    return new Cuda.CudaAccelerator(context, cudaId.DeviceId);
                case OpenCL.CLAcceleratorId clId:
                    return new OpenCL.CLAccelerator(context, clId);
                default:
                    throw new ArgumentException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator,
                        nameof(acceleratorId));
            }
        }

        /// <summary>
        /// Returns the current accelerator type.
        /// </summary>
        /// <remarks>
        /// Note that this static property is also accessible within kernels.
        /// </remarks>
        public static AcceleratorType CurrentType
        {
            [AcceleratorIntrinsic(AcceleratorIntrinsicKind.CurrentType)]
            get => AcceleratorType.CPU;
        }

        #endregion

        #region Events

        /// <summary>
        /// Will be raised if the accelerator is disposed.
        /// </summary>
        public event EventHandler Disposed;

        #endregion

        #region Instance

        /// <summary>
        /// Main object for accelerator synchronization.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object syncRoot = new object();

        /// <summary>
        /// The default memory cache for operations that require additional
        /// temporary memory.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly MemoryBufferCache memoryCache;

        /// <summary>
        /// Constructs a new accelerator.
        /// </summary>
        /// <param name="context">The target context.</param>
        /// <param name="type">The target accelerator type.</param>
        internal Accelerator(Context context, AcceleratorType type)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            AcceleratorType = type;

            AutomaticBufferDisposalEnabled = !context.HasFlags(
                ContextFlags.DisableAutomaticBufferDisposal);
            AutomaticKernelDisposalEnabled = !context.HasFlags(
                ContextFlags.DisableAutomaticKernelDisposal);
            InitKernelCache();
            InitGC();

            memoryCache = new MemoryBufferCache(this);
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
        public int MaxNumThreadsPerGroup { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        [Obsolete("Use MaxNumThreadsPerGroup instead")]
        public int MaxThreadsPerGroup => MaxNumThreadsPerGroup;

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

        /// <summary>
        /// Returns the maximum number of threads per multiprocessor.
        /// </summary>
        public int MaxNumThreadsPerMultiprocessor { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads of this accelerator.
        /// </summary>
        public int MaxNumThreads => NumMultiprocessors * MaxNumThreadsPerMultiprocessor;

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups
        /// using the maximum number of threads per group to launch common grid-stride
        /// loop kernels.
        /// </summary>
        public (Index1, Index1) MaxNumGroupsExtent =>
            (NumMultiprocessors *
                (MaxNumThreadsPerMultiprocessor / MaxNumThreadsPerGroup),
            MaxNumThreadsPerGroup);

        /// <summary>
        /// Returns the primary backend of this accelerator.
        /// </summary>
        public Backend Backend { get; private set; }

        /// <summary>
        /// Returns the default memory-buffer cache that can be used by several
        /// operations.
        /// </summary>
        public MemoryBufferCache MemoryCache => memoryCache;

        /// <summary>
        /// See <see cref="ContextFlags.DisableAutomaticBufferDisposal"/> for more
        /// information.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool AutomaticBufferDisposalEnabled { get; }

        /// <summary>
        /// See <see cref="ContextFlags.DisableAutomaticKernelDisposal"/> for more
        /// information.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool AutomaticKernelDisposalEnabled { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the current accelerator instance.
        /// </summary>
        /// <param name="backend">The backend to use.</param>
        protected void Init(Backend backend)
        {
            Backend = backend;
            OnAcceleratorCreated();
        }

        /// <summary>
        /// Invoked when the accelerator instance has been created.
        /// </summary>
        protected void OnAcceleratorCreated() => Context.OnAcceleratorCreated(this);

        /// <summary>
        /// Creates a new accelerator extension using the given provider.
        /// </summary>
        /// <typeparam name="TExtension">The type of the extension to create.</typeparam>
        /// <typeparam name="TExtensionProvider">
        /// The extension provided type to create the extension.
        /// </typeparam>
        /// <param name="provider">
        /// The extension provided to create the extension.
        /// </param>
        /// <returns>The created extension.</returns>
        public abstract TExtension CreateExtension<TExtension, TExtensionProvider>(
            TExtensionProvider provider)
            where TExtensionProvider : IAcceleratorExtensionProvider<TExtension>;

        /// <summary>
        /// Allocates a buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        public MemoryBuffer<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
        {
            // Check for blittable types
            var typeContext = Context.TypeContext;
            var elementType = typeof(T);
            var typeInfo = typeContext.GetTypeInfo(elementType);
            if (!typeInfo.IsBlittable)
            {
                throw new NotSupportedException(
                    string.Format(
                        RuntimeErrorMessages.NotSupportedNonBlittableType,
                        elementType.GetStringRepresentation()));
            }

            Bind(); return AllocateInternal<T, TIndex>(extent);
        }

        /// <summary>
        /// Allocates a buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        protected abstract MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(
            TIndex extent)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>;

        /// <summary>
        /// Allocates a 1D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 1D buffer on the this accelerator.</returns>
        public MemoryBuffer<T> Allocate<T>(int extent)
            where T : unmanaged =>
            new MemoryBuffer<T>(Allocate<T, Index1>(extent));

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer2D<T> Allocate<T>(Index2 extent)
            where T : unmanaged =>
            new MemoryBuffer2D<T>(Allocate<T, Index2>(extent));

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 2D buffer.</param>
        /// <param name="height">The height of the 2D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer2D<T> Allocate<T>(int width, int height)
            where T : unmanaged =>
            Allocate<T>(new Index2(width, height));

        /// <summary>
        /// Allocates a 3D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        public MemoryBuffer3D<T> Allocate<T>(Index3 extent)
            where T : unmanaged =>
            new MemoryBuffer3D<T>(Allocate<T, Index3>(extent));

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 3D buffer.</param>
        /// <param name="height">The height of the 3D buffer.</param>
        /// <param name="depth">The depth of the 3D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        public MemoryBuffer3D<T> Allocate<T>(int width, int height, int depth)
            where T : unmanaged =>
            Allocate<T>(new Index3(width, height, depth));

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <returns>The created accelerator stream.</returns>
        public AcceleratorStream CreateStream()
        {
            Bind(); return CreateStreamInternal();
        }

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <returns>The created accelerator stream.</returns>
        protected abstract AcceleratorStream CreateStreamInternal();

        /// <summary>
        /// Synchronizes pending operations.
        /// </summary>
        public void Synchronize()
        {
            Bind(); SynchronizeInternal();
        }

        /// <summary>
        /// Synchronizes pending operations.
        /// </summary>
        protected abstract void SynchronizeInternal();

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public override void ClearCache(ClearCacheMode mode)
        {
            lock (syncRoot)
            {
                Backend.ClearCache(mode);
                ClearKernelCache_SyncRoot();
                base.ClearCache(mode);
            }
        }

        #endregion

        #region Occupancy

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a
        /// single multiprocessor.
        /// </summary>
        /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupDim">The group dimension.</param>
        /// <returns>
        /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
        /// </returns>
        public float EstimateOccupancyPerMultiprocessor<TIndex>(
            Kernel kernel,
            TIndex groupDim)
            where TIndex : IIndex =>
            EstimateOccupancyPerMultiprocessor(kernel, groupDim.Size);

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a
        /// single multiprocessor.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <returns>
        /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
        /// </returns>
        public float EstimateOccupancyPerMultiprocessor(Kernel kernel, int groupSize) =>
            EstimateOccupancyPerMultiprocessor(kernel, groupSize, 0);

        /// <summary>
        /// Estimates the occupancy of the given kernel with the given group size of a
        /// single multiprocessor.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <returns>
        /// The estimated occupancy in percent [0.0, 1.0] of a single multiprocessor.
        /// </returns>
        public float EstimateOccupancyPerMultiprocessor(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            var maxActiveGroups = EstimateMaxActiveGroupsPerMultiprocessor(
                kernel,
                groupSize,
                dynamicSharedMemorySizeInBytes);
            return (maxActiveGroups * groupSize) / (float)MaxNumThreadsPerGroup;
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the
        /// given kernel.
        /// </summary>
        /// <typeparam name="TIndex">The index type of the group dimension.</typeparam>
        /// <param name="kernel">The kernel used for the computation of the maximum
        /// number of active groups.</param>
        /// <param name="groupDim">The group dimension.</param>
        /// <returns>
        /// The maximum number of active groups per multiprocessor for the given kernel.
        /// </returns>
        public int EstimateMaxActiveGroupsPerMultiprocessor<TIndex>(
            Kernel kernel,
            TIndex groupDim)
            where TIndex : IIndex =>
            EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupDim.Size);

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the
        /// given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum
        /// number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <returns>
        /// The maximum number of active groups per multiprocessor for the given kernel.
        /// </returns>
        public int EstimateMaxActiveGroupsPerMultiprocessor(
            Kernel kernel,
            int groupSize) =>
            EstimateMaxActiveGroupsPerMultiprocessor(kernel, groupSize, 0);

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the
        /// given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum
        /// number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <returns>
        /// The maximum number of active groups per multiprocessor for the given kernel.
        /// </returns>
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
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dynamicSharedMemorySizeInBytes));
            }
            Bind();
            return EstimateMaxActiveGroupsPerMultiprocessorInternal(
                kernel,
                groupSize,
                dynamicSharedMemorySizeInBytes);
        }

        /// <summary>
        /// Estimates the maximum number of active groups per multiprocessor for the
        /// given kernel.
        /// </summary>
        /// <param name="kernel">The kernel used for the computation of the maximum
        /// number of active groups.</param>
        /// <param name="groupSize">The number of threads per group.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <remarks>
        /// Note that the arguments do not have to be verified since they are already
        /// verified.
        /// </remarks>
        /// <returns>
        /// The maximum number of active groups per multiprocessor for the given kernel.
        /// </returns>
        protected abstract int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        public int EstimateGroupSize(Kernel kernel) =>
            EstimateGroupSize(kernel, 0, 0, out var _);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        public int EstimateGroupSize(Kernel kernel, out int minGridSize) =>
            EstimateGroupSize(kernel, 0, 0, out minGridSize);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        public int EstimateGroupSize(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            out int minGridSize) =>
            EstimateGroupSize(
                kernel,
                dynamicSharedMemorySizeInBytes,
                0,
                out minGridSize);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="computeSharedMemorySize">
        /// A callback to compute the required amount of shared memory in bytes for a
        /// given group size.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        public int EstimateGroupSize(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            out int minGridSize) =>
            EstimateGroupSize(
                kernel,
                computeSharedMemorySize,
                0,
                out minGridSize);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="computeSharedMemorySize">
        /// A callback to compute the required amount of shared memory in bytes for a
        /// given group size.
        /// </param>
        /// <param name="maxGroupSize">
        /// The maximum group-size limit on a single multiprocessor.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
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
            Bind();
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
        /// <param name="computeSharedMemorySize">
        /// A callback to compute the required amount of shared memory in bytes for a
        /// given group size.
        /// </param>
        /// <param name="maxGroupSize">
        /// The maximum group-size limit on a single multiprocessor.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <remarks>
        /// Note that the arguments do not have to be verified since they are already
        /// verified.
        /// </remarks>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        protected abstract int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize);

        /// <summary>
        /// Estimates a group size to gain maximum occupancy on this device.
        /// </summary>
        /// <param name="kernel">The kernel used for the estimation.</param>
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <param name="maxGroupSize">
        /// The maximum group-size limit on a single multiprocessor.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        public int EstimateGroupSize(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (maxGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxGroupSize));
            if (dynamicSharedMemorySizeInBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dynamicSharedMemorySizeInBytes));
            }
            Bind();
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
        /// <param name="dynamicSharedMemorySizeInBytes">
        /// The required dynamic shared-memory size in bytes.
        /// </param>
        /// <param name="maxGroupSize">
        /// The maximum group-size limit on a single multiprocessor.
        /// </param>
        /// <param name="minGridSize">
        /// The minimum grid size to gain maximum occupancy on this device.
        /// </param>
        /// <remarks>
        /// Note that the arguments do not have to be verified since they are already
        /// verified.
        /// </remarks>
        /// <returns>
        /// An estimated group size to gain maximum occupancy on this device.
        /// </returns>
        protected abstract int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed?.Invoke(this, EventArgs.Empty);
                if (currentAccelerator == this)
                {
                    OnUnbind();
                    currentAccelerator = null;
                }

                memoryCache.Dispose();

                DisposeChildObjects();
                DisposeGC();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this accelerator.
        /// </summary>
        /// <returns>The string representation of this accelerator.</returns>
        public override string ToString() =>
            $"{Name} [WarpSize: {WarpSize}, " +
            $"MaxNumThreadsPerGroup: {MaxNumThreadsPerGroup}, " +
            $"MemorySize: {MemorySize}]";

        #endregion
    }
}
