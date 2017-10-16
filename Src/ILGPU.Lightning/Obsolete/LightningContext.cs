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
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a convenient wrapper for accelerators and operations on accelerators.
    /// A lightning context allows for high-level programming in a convenient way.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [Obsolete("Use the accelerator class instead. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
    public sealed partial class LightningContext : DisposeBase
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private CompileUnit compileUnit;

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
            Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
#if DEBUG
            if (Debugger.IsAttached)
                flags |= CompileUnitFlags.EnableAssertions;
#endif
            compileUnit = accelerator.Context.CreateCompileUnit(accelerator.Backend, flags);
            CompiledKernelCache = new CompiledKernelCache(accelerator);
            DisposeAccelerator = disposeAccelerator;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated ILGPU context.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Context Context => Accelerator.Context;

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        [Obsolete("This property will not be supported in the future. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Accelerator Accelerator { get; private set; }

        /// <summary>
        /// Returns the internal backend of this lightning context.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Backend Backend => Accelerator.Backend;

        /// <summary>
        /// Returns the internal compile unit of this lightning context.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public CompileUnit CompileUnit => compileUnit;

        /// <summary>
        /// Returns the default kernel cache for compiled kernels.
        /// </summary>
        [Obsolete("This property will not be supported in the future. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public CompiledKernelCache CompiledKernelCache { get; }

        /// <summary>
        /// Returns the default buffer cache that is used by several operations.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBufferCache DefaultCache => Accelerator.MemoryCache;

        /// <summary>
        /// Return true iff the associated accelerator should be automatically disposed upon disposal of this context.
        /// </summary>
        [Obsolete("This property will not be supported in the future. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public bool DisposeAccelerator { get; }

        #region Wrapped Properties

        /// <summary>
        /// Returns the default stream of this accelerator.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public AcceleratorStream DefaultStream => Accelerator.DefaultStream;

        /// <summary>
        /// Returns the type of the accelerator.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public AcceleratorType AcceleratorType => Accelerator.AcceleratorType;

        /// <summary>
        /// Returns the memory size in bytes.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public long MemorySize => Accelerator.MemorySize;

        /// <summary>
        /// Returns the accelerators for which the peer access has been enabled.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public IReadOnlyCollection<Accelerator> PeerAccelerators => Accelerator.PeerAccelerators;

        /// <summary>
        /// Returns the name of the device.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public string Name => Accelerator.Name;

        /// <summary>
        /// Returns the max grid size.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Index3 MaxGridSize => Accelerator.MaxGridSize;

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        [Obsolete("Use MaxNumThreadsPerGroup instead")]
        public int MaxThreadsPerGroup => Accelerator.MaxThreadsPerGroup;

        /// <summary>
        /// Returns the maximum number of shared memory per thread group in bytes.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public int MaxSharedMemoryPerGroup => Accelerator.MaxSharedMemoryPerGroup;

        /// <summary>
        /// Returns the maximum number of constant memory in bytes.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public int MaxConstantMemory => Accelerator.MaxConstantMemory;

        /// <summary>
        /// Return the warp size.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public int WarpSize => Accelerator.WarpSize;

        /// <summary>
        /// Returns the number of available multiprocessors.
        /// </summary>
        [Obsolete("Use accelerator properties. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public int NumMultiprocessors => Accelerator.NumMultiprocessors;

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="method">The method to compile into a <see cref="CompiledKernel"/> .</param>
        /// <returns>The compiled kernel.</returns>
        [Obsolete("Use Accelerator.CompileKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public CompiledKernel CompileKernel(MethodInfo method)
        {
            return Accelerator.CompileKernel(method);
        }

        /// <summary>
        /// Allocates a buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : struct
            where TIndex : struct, IIndex, IGenericIndex<TIndex>
        {
            return Accelerator.Allocate<T, TIndex>(extent);
        }

        /// <summary>
        /// Allocates a 1D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 1D buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer<T> Allocate<T>(int extent)
            where T : struct
        {
            return Accelerator.Allocate<T>(extent);
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer2D<T> Allocate<T>(Index2 extent)
            where T : struct
        {
            return Accelerator.Allocate<T>(extent);
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 2D buffer.</param>
        /// <param name="height">The height of the 2D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer2D<T> Allocate<T>(int width, int height)
            where T : struct
        {
            return Accelerator.Allocate<T>(width, height);
        }

        /// <summary>
        /// Allocates a 3D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer3D<T> Allocate<T>(Index3 extent)
            where T : struct
        {
            return Accelerator.Allocate<T>(extent);
        }

        /// <summary>
        /// Allocates a 2D buffer with the specified number of elements on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 3D buffer.</param>
        /// <param name="height">The height of the 3D buffer.</param>
        /// <param name="depth">The depth of the 3D buffer.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        [Obsolete("Use Accelerator.Allocate. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public MemoryBuffer3D<T> Allocate<T>(int width, int height, int depth)
            where T : struct
        {
            return Accelerator.Allocate<T>(width, height, depth);
        }

        /// <summary>
        /// Creates a new accelerator stream.
        /// </summary>
        /// <returns>The created accelerator stream.</returns>
        [Obsolete("Use Accelerator.CreateStream. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public AcceleratorStream CreateStream()
        {
            return Accelerator.CreateStream();
        }

        /// <summary>
        /// Synchronizes pending operations.
        /// </summary>
        [Obsolete("Use Accelerator.Synchronize. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Synchronize()
        {
            Accelerator.Synchronize();
        }

        /// <summary>
        /// Makes the underlying accelerator the current one for this thread.
        /// </summary>
        [Obsolete("Use Accelerator.MakeCurrent. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void MakeCurrent()
        {
            Accelerator.MakeCurrent();
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
        protected override void Dispose(bool disposing)
        {
            if (DisposeAccelerator && Accelerator != null)
            {
                Accelerator.Dispose();
                Accelerator = null;
            }
            Dispose(ref compileUnit);
        }

        #endregion
    }
}
