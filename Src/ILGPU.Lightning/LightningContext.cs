// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Compiler;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a convenient wrapper for accelerators and operations on accelerators.
    /// A lightning context allows for high-level programming in a convenient way.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class LightningContext : DisposeBase
    {
        #region Constants

        /// <summary>
        /// Represents the name of the native library.
        /// </summary>
#if WIN
        internal const string NativeLibName = "ILGPU.Lightning.Native.dll";
#else
        internal const string NativeLibName = "ILGPU.Lightning.Native.so";
#endif

        #endregion

        #region Static

        /// <summary>
        /// Represents the default flags of a new lightning context.
        /// </summary>
        public static readonly CompileUnitFlags DefaultFlags =
            CompileUnitFlags.FastMath |
            CompileUnitFlags.UseGPUMath;

        /// <summary>
        /// Initializes the native ILGPU.Lightning library.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static LightningContext()
        {
            DLLLoader.LoadLib(NativeLibName);
        }

        /// <summary>
        /// Returns a list of available accelerators.
        /// </summary>
        public static IReadOnlyList<AcceleratorId> Accelerators => Accelerator.Accelerators;

        // Generic

        /// <summary>
        /// Constructs a LightningContext with an associated accelerator id.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The specified accelerator id.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateContext(Context context, AcceleratorId acceleratorId)
        {
            return new LightningContext(Accelerator.Create(context, acceleratorId), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated accelerator id.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The specified accelerator id.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateContext(
            Context context,
            AcceleratorId acceleratorId,
            CompileUnitFlags flags)
        {
            return new LightningContext(Accelerator.Create(context, acceleratorId), flags, true);
        }

        // CPU

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context)
        {
            return new LightningContext(new CPUAccelerator(context), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, ThreadPriority threadPriority)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, threadPriority), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize, ThreadPriority threadPriority)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize, threadPriority), true);
        }

        // Cuda

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator targeting the default device.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context)
        {
            return new LightningContext(new CudaAccelerator(context), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="flags">The accelerator flags.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId, CudaAcceleratorFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId, flags), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="flags">The accelerator flags.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device, CudaAcceleratorFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device, flags), true);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// A cached kernel key.
        /// </summary>
        private struct CachedKernelKey : IEquatable<CachedKernelKey>
        {
            #region Instance

            /// <summary>
            /// Constructs a new kernel key.
            /// </summary>
            /// <param name="method">The kernel method.</param>
            /// <param name="implicitGroupSize">The implicit group size (if any).</param>
            public CachedKernelKey(
                MethodInfo method,
                int? implicitGroupSize)
            {
                Method = method;
                ImplicitGroupSize = implicitGroupSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated kernel method.
            /// </summary>
            public MethodInfo Method { get; }

            /// <summary>
            /// Returns the associated implicit group size (if any).
            /// </summary>
            public int? ImplicitGroupSize { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true iff the given cached key is equal to the current one.
            /// </summary>
            /// <param name="key">The other key.</param>
            /// <returns>True, iff the given cached key is equal to the current one.</returns>
            public bool Equals(CachedKernelKey key)
            {
                return key.Method == Method &&
                    key.ImplicitGroupSize == ImplicitGroupSize;
            }

            #endregion

            #region Object

            public override int GetHashCode()
            {
                return Method.GetHashCode() ^ (ImplicitGroupSize ?? 1);
            }

            public override bool Equals(object obj)
            {
                if (obj is CachedKernelKey)
                    return Equals((CachedKernelKey)obj);
                return false;
            }

            public override string ToString()
            {
                return Method.ToString();
            }

            #endregion
        }

        /// <summary>
        /// A cached kernel.
        /// </summary>
        private struct CachedKernel
        {
            #region Instance

            /// <summary>
            /// Constructs a new cached kernel.
            /// </summary>
            /// <param name="kernel">The kernel to cache.</param>
            public CachedKernel(Kernel kernel)
            {
                Kernel = kernel;
                GroupSize = 0;
                MinGridSize = 0;
            }

            /// <summary>
            /// Constructs a new cached kernel.
            /// </summary>
            /// <param name="kernel">The kernel to cache.</param>
            /// <param name="groupSize">The computed group size.</param>
            /// <param name="minGridSize">The computed minimum grid size.</param>
            public CachedKernel(
                Kernel kernel,
                int groupSize,
                int minGridSize)
            {
                Kernel = kernel;
                GroupSize = groupSize;
                MinGridSize = minGridSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the cached kernel.
            /// </summary>
            public Kernel Kernel { get; }

            /// <summary>
            /// Returns the computed group size.
            /// </summary>
            public int GroupSize { get; }

            /// <summary>
            /// Returns the computed minimum grid size.
            /// </summary>
            public int MinGridSize { get; }

            #endregion
        }

        /// <summary>
        /// Represents a generic kernel loader.
        /// </summary>
        private interface IKernelLoader
        {
            /// <summary>
            /// Loads the given kernel using the given accelerator.
            /// </summary>
            /// <param name="accelerator">The target accelerator for the loading operation.</param>
            /// <param name="compiledKernel">The compiled kernel to load.</param>
            /// <returns>The loaded kernel.</returns>
            CachedKernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel);

            /// <summary>
            /// Fetches information from the given cached kernel.
            /// </summary>
            /// <param name="cachedKernel">The cached kernel.</param>
            void FetchInformation(CachedKernel cachedKernel);
        }

        /// <summary>
        /// Represents a launcher provider to create launcher delegates.
        /// </summary>
        private interface ILauncherProvider
        {
            /// <summary>
            /// Creates a launcher delegate for the given kernel.
            /// </summary>
            /// <typeparam name="TDelegate">The delegate type.</typeparam>
            /// <param name="kernel">The kernel for the creation operation.</param>
            /// <returns>A launcher delegate for the given kernel.</returns>
            TDelegate CreateLauncher<TDelegate>(Kernel kernel)
                where TDelegate : class;
        }

        #endregion

        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Accelerator accelerator;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Backend backend;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private CompileUnit compileUnit;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private CompiledKernelCache compiledKernelCache;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MemoryBufferCache defaultCache;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<CachedKernelKey, CachedKernel> kernelCache =
            new Dictionary<CachedKernelKey, CachedKernel>();

        /// <summary>
        /// Constructs a new lightning context without disposing the associated accelerator upon disposal of this context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator on which all operations are performed.</param>
        public LightningContext(Accelerator accelerator)
            : this(accelerator, DefaultFlags, false)
        { }

        /// <summary>
        /// Constructs a new lightning context without disposing the associated accelerator upon disposal of this context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator on which all operations are performed.</param>
        /// <param name="flags">The compile-unit flags.</param>
        public LightningContext(Accelerator accelerator, CompileUnitFlags flags)
            : this(accelerator, flags, false)
        { }

        /// <summary>
        /// Constructs a new lightning context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator on which all operations are performed.</param>
        /// <param name="disposeAccelerator">True, iff the associated accelerator should be automatically disposed upon disposal of this context.</param>
        public LightningContext(Accelerator accelerator, bool disposeAccelerator)
            : this(accelerator, DefaultFlags, disposeAccelerator)
        { }

        /// <summary>
        /// Constructs a new lightning context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator on which all operations are performed.</param>
        /// <param name="flags">The compile-unit flags.</param>
        /// <param name="disposeAccelerator">True, iff the associated accelerator should be automatically disposed upon disposal of this context.</param>
        public LightningContext(Accelerator accelerator, CompileUnitFlags flags, bool disposeAccelerator)
        {
            this.accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            backend = accelerator.CreateBackend();
            Debug.Assert(backend != null);
#if DEBUG
            if (Debugger.IsAttached)
                flags |= CompileUnitFlags.EnableAssertions;
#endif
            compileUnit = accelerator.Context.CreateCompileUnit(backend, flags);
            compiledKernelCache = new CompiledKernelCache(this);
            defaultCache = new MemoryBufferCache(accelerator);
            DisposeAccelerator = disposeAccelerator;

            InitScan();
            InitRadixSort();
            InitRadixSortPairs();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated ILGPU context.
        /// </summary>
        public Context Context => Accelerator.Context;

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator => accelerator;

        /// <summary>
        /// Returns the internal backend of this lightning context.
        /// </summary>
        public Backend Backend => backend;

        /// <summary>
        /// Returns the internal compile unit of this lightning context.
        /// </summary>
        public CompileUnit CompileUnit => compileUnit;

        /// <summary>
        /// Returns the default kernel cache for compiled kernels.
        /// </summary>
        public CompiledKernelCache CompiledKernelCache => compiledKernelCache;

        /// <summary>
        /// Returns the default buffer cache that is used by several operations.
        /// </summary>
        public MemoryBufferCache DefaultCache => defaultCache;

        /// <summary>
        /// Return true iff the associated accelerator should be automatically disposed upon disposal of this context.
        /// </summary>
        public bool DisposeAccelerator { get; }

        #region Wrapped Properties

        /// <summary>
        /// Returns the default stream of this accelerator.
        /// </summary>
        public AcceleratorStream DefaultStream => accelerator.DefaultStream;

        /// <summary>
        /// Returns the type of the accelerator.
        /// </summary>
        public AcceleratorType AcceleratorType => Accelerator.AcceleratorType;

        /// <summary>
        /// Returns the memory size in bytes.
        /// </summary>
        public long MemorySize => Accelerator.MemorySize;

        /// <summary>
        /// Returns the accelerators for which the peer access has been enabled.
        /// </summary>
        public IReadOnlyCollection<Accelerator> PeerAccelerators => Accelerator.PeerAccelerators;

        /// <summary>
        /// Returns the name of the device.
        /// </summary>
        public string Name => Accelerator.Name;

        /// <summary>
        /// Returns the max grid size.
        /// </summary>
        public Index3 MaxGridSize => Accelerator.MaxGridSize;

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        public int MaxThreadsPerGroup => Accelerator.MaxThreadsPerGroup;

        /// <summary>
        /// Returns the maximum number of shared memory per thread group in bytes.
        /// </summary>
        public int MaxSharedMemoryPerGroup => Accelerator.MaxSharedMemoryPerGroup;

        /// <summary>
        /// Returns the maximum number of constant memory in bytes.
        /// </summary>
        public int MaxConstantMemory => Accelerator.MaxConstantMemory;

        /// <summary>
        /// Return the warp size.
        /// </summary>
        public int WarpSize => Accelerator.WarpSize;

        /// <summary>
        /// Returns the number of available multiprocessors.
        /// </summary>
        public int NumMultiprocessors => Accelerator.NumMultiprocessors;

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="method">The method to compile into a <see cref="CompiledKernel"/> .</param>
        /// <returns>The compiled kernel.</returns>
        public CompiledKernel CompileKernel(MethodInfo method)
        {
            return compiledKernelCache.CompileKernel(method);
        }

        /// <summary>
        /// Loads a kernel specified by the given method.
        /// </summary>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The custom group size for implicitly-grouped kernels (if any).</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <returns>The loaded kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Kernel LoadGenericKernel<TKernelLoader>(
            MethodInfo method,
            int? groupSize,
            ref TKernelLoader kernelLoader)
            where TKernelLoader : struct, IKernelLoader
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            var cachedKey = new CachedKernelKey(method, groupSize);
            if (kernelCache.TryGetValue(cachedKey, out CachedKernel result))
            {
                kernelLoader.FetchInformation(result);
                return result.Kernel;
            }
            var compiledKernel = CompileKernel(method);
            var kernel = kernelLoader.LoadKernel(Accelerator, compiledKernel);
            kernelCache.Add(cachedKey, kernel);
            return kernel.Kernel;
        }

        /// <summary>
        /// Loads a kernel specified by the given method and returns a launcher of the specified type.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <typeparam name="TLauncherProvider">The type of the custom launcher provider.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The custom group size for implicitly-grouped kernels (if any).</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <param name="launcherProvider">The launcher provider.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate LoadGenericKernel<TDelegate, TKernelLoader, TLauncherProvider>(
            MethodInfo method,
            int? groupSize,
            ref TKernelLoader kernelLoader,
            ref TLauncherProvider launcherProvider)
            where TDelegate : class
            where TKernelLoader : struct, IKernelLoader
            where TLauncherProvider : struct, ILauncherProvider
        {
            var kernel = LoadGenericKernel(method, groupSize, ref kernelLoader);
            return launcherProvider.CreateLauncher<TDelegate>(kernel);
        }

        /// <summary>
        /// Allocates a buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        public MemoryBuffer<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : struct
            where TIndex : struct, IIndex, IGenericIndex<TIndex>
        {
            return accelerator.Allocate<T, TIndex>(extent);
        }

        /// <summary>
        /// Allocates a 1D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 1D buffer on the this accelerator.</returns>
        public MemoryBuffer<T> Allocate<T>(int extent)
            where T : struct
        {
            return accelerator.Allocate<T>(extent);
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
            return accelerator.Allocate<T>(extent);
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
            return accelerator.Allocate<T>(width, height);
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
            return accelerator.Allocate<T>(extent);
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
            return accelerator.Allocate<T>(width, height, depth);
        }


        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <returns>The created accelerator stream.</returns>
        public AcceleratorStream CreateStream()
        {
            return accelerator.CreateStream();
        }

        /// <summary>
        /// Synchronizes pending operations.
        /// </summary>
        public void Synchronize()
        {
            accelerator.Synchronize();
        }

        /// <summary>
        /// Makes the underlying accelerator the current one for this thread.
        /// </summary>
        public void MakeCurrent()
        {
            accelerator.MakeCurrent();
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of the wrapped accelerator.
        /// </summary>
        /// <returns>The string representation of the wrapped accelerator.</returns>
        public override string ToString()
        {
            return Accelerator.ToString();
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "defaultCache", Justification = "Dispose method will be invoked by a helper method")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "compiledKernelCache", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            DisposeScan();
            DisposeRadixSort();
            DisposeRadixSortPairs();
            foreach (var kernel in kernelCache.Values)
                kernel.Kernel.Dispose();
            kernelCache.Clear();
            Dispose(ref compiledKernelCache);
            Dispose(ref defaultCache);
            Dispose(ref compileUnit);
            Dispose(ref backend);
            if (DisposeAccelerator)
                Dispose(ref accelerator);
        }

        #endregion
    }
}
