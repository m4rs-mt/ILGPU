// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreadWiseRNG.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Represents a single RNG instance per thread stored separately in a memory buffer.
    /// </summary>
    /// <typeparam name="TRandomProvider">The underlying RNG provider type.</typeparam>
    [CLSCompliant(false)]
    public class ThreadWiseRNG<TRandomProvider> : DisposeBase
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        #region Instance
        
        /// <summary>
        /// Stores a single RNG instance per thread.
        /// </summary>
        private readonly MemoryBuffer1D<TRandomProvider, Stride1D.Dense> randomProviders;
        
        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="maxNumThreads">The maximum number of parallel threads.</param>
        /// <param name="random">The parent RNG provider.</param>
        /// <param name="numInitializers">
        /// The maximum number of initializers used on the CPU side.
        /// </param>
        public ThreadWiseRNG(
            Accelerator accelerator,
            AcceleratorStream stream,
            LongIndex1D maxNumThreads,
            System.Random random,
            int numInitializers = ushort.MaxValue)
        {
            if (maxNumThreads < 1L)
                throw new ArgumentOutOfRangeException(nameof(maxNumThreads));

            // Allocate all random providers
            randomProviders = accelerator.Allocate1D<TRandomProvider>(maxNumThreads);
            accelerator.InitRNGView(stream, RNGView, random, numInitializers);
        }
        
        #endregion
        
        #region Methods

        /// <summary>
        /// Returns the underlying RNG view.
        /// </summary>
        public ArrayView<TRandomProvider> RNGView => randomProviders.View;

        #endregion
        
        #region IDisposable

        /// <summary>
        /// Frees the underlying RNG buffers.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                randomProviders.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
