// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2020 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: CuBlas.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wraps library calls to the external native Nvidia cuBlas library.
    /// </summary>
    public sealed unsafe partial class CuBlas : DisposeBase
    {
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
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));

            accelerator.Bind();
            CuBlasException.ThrowIfFailed(
                NativeMethods.Create(out IntPtr handle));
            Handle = handle;

            CuBlasException.ThrowIfFailed(
                NativeMethods.GetVersion(handle, out int version));
            Version = version;

            Stream = accelerator.DefaultStream as CudaStream;
        }

        #endregion

        #region Properties

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
                    NativeMethods.GetPointerMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    NativeMethods.SetPointerMode(Handle, value));
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
                    NativeMethods.GetAtomicsMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    NativeMethods.SetAtomicsMode(Handle, value));
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
                    NativeMethods.GetMathMode(Handle, out var mode));
                return mode;
            }
            set
            {
                CuBlasException.ThrowIfFailed(
                    NativeMethods.SetMathMode(Handle, value));
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
                    NativeMethods.SetStream(Handle, value.StreamPtr));
                stream = value;
            }
        }

        #endregion

        #region Methods

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (Handle != IntPtr.Zero)
            {
                CuBlasException.ThrowIfFailed(
                    NativeMethods.Free(Handle));
                Handle = IntPtr.Zero;
            }
        }

        #endregion
    }
}
