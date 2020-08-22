// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an unmanaged OpenCL buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public sealed class CLMemoryBuffer<T, TIndex> : MemoryBuffer<T, TIndex>
        where T : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
    {
        #region Instance

        /// <summary>
        /// Constructs a new OpenCL buffer.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="extent">The extent.</param>
        internal CLMemoryBuffer(CLAccelerator accelerator, TIndex extent)
            : base(accelerator, extent)
        {
            CLException.ThrowIfFailed(
                CurrentAPI.CreateBuffer(
                    accelerator.ContextPtr,
                    CLBufferFlags.CL_MEM_READ_WRITE,
                    new IntPtr(extent.Size * ElementSize),
                    IntPtr.Zero,
                    out IntPtr resultPtr));
            NativePtr = resultPtr;
        }

        #endregion

        #region Methods

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyToView(
        /// AcceleratorStream, ArrayView{T}, LongIndex1)"/>
        protected internal unsafe override void CopyToView(
            AcceleratorStream stream,
            ArrayView<T> target,
            LongIndex1 sourceOffset)
        {
            var clStream = (CLStream)stream;

            switch (target.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    CLException.ThrowIfFailed(
                        CurrentAPI.ReadBuffer(
                            clStream.CommandQueue,
                            NativePtr,
                            false,
                            new IntPtr(sourceOffset * ElementSize),
                            new IntPtr(target.LengthInBytes),
                            new IntPtr(target.LoadEffectiveAddress())));
                    break;
                case AcceleratorType.OpenCL:
                    CLException.ThrowIfFailed(
                        CurrentAPI.CopyBuffer(
                            clStream.CommandQueue,
                            NativePtr,
                            target.Source.NativePtr,
                            new IntPtr(sourceOffset * ElementSize),
                            new IntPtr(target.Index * ElementSize),
                            new IntPtr(target.LengthInBytes)));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer{T, TIndex}.CopyFromView(
        /// AcceleratorStream, ArrayView{T}, LongIndex1)"/>
        protected internal unsafe override void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            LongIndex1 targetOffset)
        {
            var clStream = (CLStream)stream;

            switch (source.AcceleratorType)
            {
                case AcceleratorType.CPU:
                    CLException.ThrowIfFailed(
                        CurrentAPI.WriteBuffer(
                            clStream.CommandQueue,
                            NativePtr,
                            false,
                            new IntPtr(targetOffset * ElementSize),
                            new IntPtr(source.LengthInBytes),
                            new IntPtr(source.LoadEffectiveAddress())));
                    break;
                case AcceleratorType.OpenCL:
                    CLException.ThrowIfFailed(
                        CurrentAPI.CopyBuffer(
                            clStream.CommandQueue,
                            source.Source.NativePtr,
                            NativePtr,
                            new IntPtr(source.Index * ElementSize),
                            new IntPtr(targetOffset * ElementSize),
                            new IntPtr(source.LengthInBytes)));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary cref="MemoryBuffer.MemSetToZero(AcceleratorStream)"/>
        public override void MemSetToZero(AcceleratorStream stream) =>
            CLException.ThrowIfFailed(
                CurrentAPI.FillBuffer<byte>(
                    ((CLStream)stream).CommandQueue,
                    NativePtr,
                    0,
                    IntPtr.Zero,
                    new IntPtr(LengthInBytes)));

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (NativePtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(
                    CurrentAPI.ReleaseBuffer(NativePtr));
                NativePtr = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
