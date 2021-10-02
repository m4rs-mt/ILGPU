// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuBlas.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wraps library calls to the external native Nvidia cuBlas library.
    /// </summary>
    /// <typeparam name="TPointerModeHandler">
    /// A user-defined handler type to change/adapt the current pointer mode.
    /// </typeparam>
    public unsafe partial class CuBlas<TPointerModeHandler> : DisposeBase
        where TPointerModeHandler : struct, ICuBlasPointerModeHandler<TPointerModeHandler>
    {
        #region Nested Types

        /// <summary>
        /// Represents a scoped assignment of a <see cref="CuBlas{TPointerModeHandler}.
        /// PointerMode"/> value.
        /// </summary>
        public readonly struct PointerModeScope : IDisposable
        {
            #region Instance

            /// <summary>
            /// Constructs a new pointer scope.
            /// </summary>
            /// <param name="parent">The parent pointer scope.</param>
            /// <param name="pointerMode">The new pointer mode.</param>
            internal PointerModeScope(
                CuBlas<TPointerModeHandler> parent,
                CuBlasPointerMode pointerMode)
            {
                Debug.Assert(parent != null, "Invalid parent");

                Parent = parent;
                OldPointerMode = parent.PointerMode;
                parent.PointerMode = pointerMode;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent <see cref="CuBlas{TPointerModeHandler}"/> instance.
            /// </summary>
            public CuBlas<TPointerModeHandler> Parent { get; }

            /// <summary>
            /// Returns the old pointer mode.
            /// </summary>
            public CuBlasPointerMode OldPointerMode { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Recovers the previous pointer mode.
            /// </summary>
            public void Recover() => Parent.PointerMode = OldPointerMode;

            #endregion

            #region IDisposable

            /// <summary>
            /// Restores the previous pointer mode.
            /// </summary>
            void IDisposable.Dispose() => Recover();

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Loads a native address.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The array view.</param>
        /// <returns>The native unsafe address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void* LoadCuBlasAddress<T>(ArrayView<T> view)
            where T : unmanaged =>
            view.LoadEffectiveAddressAsPtr().ToPointer();

        #endregion

        #region Instance

        /// <summary>
        /// The underlying associated stream.
        /// </summary>
        private CudaStream stream;

        /// <summary>
        /// Constructs a new CuBlas instance to access the Nvidia cublas library.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        public CuBlas(CudaAccelerator accelerator)
            : this(accelerator, new CuBlasAPIVersion?())
        { }

        /// <summary>
        /// Constructs a new CuBlas instance to access the Nvidia cublas library.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        /// <param name="apiVersion">The cuBlas API version.</param>
        public CuBlas(CudaAccelerator accelerator, CuBlasAPIVersion apiVersion)
            : this(accelerator, new CuBlasAPIVersion?(apiVersion))
        { }

        /// <summary>
        /// Constructs a new CuBlas instance to access the Nvidia cublas library.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        /// <param name="apiVersion">The cuBlas API version.</param>
        private CuBlas(CudaAccelerator accelerator, CuBlasAPIVersion? apiVersion)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));

            API = CuBlasAPI.Create(apiVersion);
            accelerator.Bind();
            CuBlasException.ThrowIfFailed(
                API.Create(out IntPtr handle));
            Handle = handle;

            CuBlasException.ThrowIfFailed(
                API.GetVersion(handle, out int currentVersion));
            Version = currentVersion;

            Stream = accelerator.DefaultStream as CudaStream;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated cuBlas API instance.
        /// </summary>
        internal CuBlasAPI API { get; }

        /// <summary>
        /// The native CuBlas library handle.
        /// </summary>
        public IntPtr Handle { get; private set; }

        /// <summary>
        /// Returns the current library version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Gets or sets the current <see cref="CuBlasPointerMode"/> value.
        /// </summary>
        public CuBlasPointerMode PointerMode
        {
            get
            {
                CuBlasException.ThrowIfFailed(
                    API.GetPointerMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    API.SetPointerMode(Handle, value));
            }
        }

        /// <summary>
        /// Gets or sets the current <see cref="CuBlasAtomicsMode"/> value.
        /// </summary>
        public CuBlasAtomicsMode AtomicsMode
        {
            get
            {
                CuBlasException.ThrowIfFailed(
                    API.GetAtomicsMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    API.SetAtomicsMode(Handle, value));
            }
        }

        /// <summary>
        /// Gets or sets the current <see cref="CuBlasMathMode"/> value.
        /// </summary>
        public CuBlasMathMode MathMode
        {
            get
            {
                CuBlasException.ThrowIfFailed(
                    API.GetMathMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    API.SetMathMode(Handle, value));
            }
        }

        /// <summary>
        /// Gets or sets the associated accelerator stream.
        /// </summary>
        public CudaStream Stream
        {
            get => stream;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                CuBlasException.ThrowIfFailed(
                    API.SetStream(Handle, value.StreamPtr));
                stream = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Opens a new scoped pointer mode.
        /// </summary>
        /// <param name="pointerMode">The new pointer mode to use.</param>
        /// <returns>The created pointer scope.</returns>
        public PointerModeScope BeginPointerScope(CuBlasPointerMode pointerMode) =>
            new PointerModeScope(this, pointerMode);

        /// <summary>
        /// Ensures the given pointer mode.
        /// </summary>
        /// <param name="pointerMode">The pointer mode to ensure.</param>
        /// <remarks>
        /// Checks whether the given mode is compatible with the current one in debug
        /// builds.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsurePointerMode(CuBlasPointerMode pointerMode)
        {
            TPointerModeHandler pointerModeHandler = default;
            pointerModeHandler.UpdatePointerMode(this, pointerMode);
        }

        /// <summary>
        /// Ensures that the accelerator for this CuBlas instance is made current.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureAcceleratorBinding() =>
            Stream.Accelerator.Bind();

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (Handle != IntPtr.Zero)
            {
                CuBlasException.ThrowIfFailed(
                    API.Free(Handle));
                Handle = IntPtr.Zero;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a <see cref="CuBlas{TPointerModeHandler}"/> class that does not handle
    /// pointer mode changes automatically.
    /// </summary>
    public sealed class CuBlas : CuBlas<CuBlasPointerModeHandlers.ManualMode>
    {
        #region Instance

        /// <summary>
        /// Constructs a new CuBlas instance to access the Nvidia cublas library.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        public CuBlas(CudaAccelerator accelerator)
            : base(accelerator)
        { }

        /// <summary>
        /// Constructs a new CuBlas instance to access the Nvidia cublas library.
        /// </summary>
        /// <param name="accelerator">The associated cuda accelerator.</param>
        /// <param name="apiVersion">The cuBlas API version.</param>
        public CuBlas(CudaAccelerator accelerator, CuBlasAPIVersion apiVersion)
            : base(accelerator, apiVersion)
        { }

        #endregion
    }
}
