// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the base interface of all memory buffers.
    /// </summary>
    public interface IMemoryBuffer : IAcceleratorObject
    {
        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        void MemSetToZero(AcceleratorStream stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        byte[] GetAsRawArray(AcceleratorStream stream);
    }

    /// <summary>
    /// Represents the generic base interface of all typed memory buffers.
    /// </summary>
    public interface IMemoryBuffer<T> : IMemoryBuffer
        where T : struct
    {
        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        Index LengthInBytes { get; }

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        T[] GetAsArray(AcceleratorStream stream);
    }

    /// <summary>
    /// Represents the generic base interface of all memory buffers
    /// using n-dimensional indices.
    /// </summary>
    public interface IMemoryBuffer<T, TIndex> : IMemoryBuffer<T>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        ArrayView<T, TIndex> View { get; }

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        TIndex Extent { get; }

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        T[] GetAsArray(AcceleratorStream stream, TIndex offset, TIndex extent);

        /// <summary>
        /// Copies elements from the current buffer to the target view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        void CopyTo(
            AcceleratorStream stream,
            ArrayView<T, TIndex> target,
            TIndex sourceOffset);

        /// <summary>
        /// Copies elements to the current buffer from the source view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        void CopyFrom(
            AcceleratorStream stream,
            ArrayView<T, TIndex> source,
            TIndex targetOffset);

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <returns>The new subview.</returns>
        ArrayView<T, TIndex> GetSubView(TIndex offset);

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        ArrayView<T, TIndex> GetSubView(TIndex offset, TIndex subViewExtent);

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        ArrayView<T, TIndex> ToArrayView();
    }
}
