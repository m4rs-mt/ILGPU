// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ExchangeBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// A static helper class for the class <see cref="ExchangeBuffer{T}"/>.
    /// </summary>
    public static class ExchangeBuffer
    {
        /// <summary>
        /// Allocates a new exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers
        /// between the CPU and the GPU.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>The allocated exchange buffer.</returns>
        /// <remarks>
        /// This function uses the default buffer allocation mode
        /// <see cref="ExchangeBufferMode.PreferPagedLockedMemory"/>
        /// </remarks>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index1 extent)
            where T : unmanaged =>
            accelerator.AllocateExchangeBuffer<T>(
                extent,
                ExchangeBufferMode.PreferPagedLockedMemory);

        /// <summary>
        /// Allocates a new exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <param name="mode">The current allocation mode.</param>
        /// <returns>The allocated exchange buffer.</returns>
        public static ExchangeBuffer<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index1 extent,
            ExchangeBufferMode mode)
            where T : unmanaged
        {
            var gpuBuffer = accelerator.Allocate<T>(extent);
            return new ExchangeBuffer<T>(gpuBuffer, mode);
        }

        /// <summary>
        /// Allocates a new 2D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>The allocated 2D exchange buffer.</returns>
        /// <remarks>
        /// This function uses the default buffer allocation mode
        /// <see cref="ExchangeBufferMode.PreferPagedLockedMemory"/>
        /// </remarks>
        public static ExchangeBuffer2D<T> AllocateExchangeBuffer<T>(
            this Accelerator accelerator,
            Index2 extent)
            where T : unmanaged => accelerator.AllocateExchangeBuffer<T>(extent, ExchangeBufferMode.PreferPagedLockedMemory);

        /// <summary>
        /// Allocates a new 2D exchange buffer that allocates the specified amount of
        /// elements on the current accelerator. Furthermore, it keeps a buffer of the
        /// same size in pinned CPU memory to enable asynchronous memory transfers
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="accelerator">The associated accelerator to use.</param>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <param name="mode">The exchange buffer mode to use.</param>
        /// <returns>The allocated 2D exchange buffer.</returns>
        public static ExchangeBuffer2D<T> AllocateExchangeBuffer<T> (
            this Accelerator accelerator,
            Index2 extent,
            ExchangeBufferMode mode)
            where T : unmanaged
        {
            var gpuBuffer = accelerator.Allocate<T>(extent);
            return new ExchangeBuffer2D<T>(gpuBuffer, mode);
        }
    }

    /// <summary>
    /// Specifies the allocation mode for a single exchange buffer.
    /// </summary>
    public enum ExchangeBufferMode
    {
        /// <summary>
        /// Prefer paged locked memory for improved transfer speeds.
        /// </summary>
        PreferPagedLockedMemory = 0,

        /// <summary>
        /// Allocate CPU memory in pageable memory to leverage virtual memory.
        /// </summary>
        UsePageablememory = 1,
    }
}
