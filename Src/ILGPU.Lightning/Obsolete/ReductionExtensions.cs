// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReductionExtensions.cs (obsolete)
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.ReductionOperations;
using ILGPU.Runtime;
using ILGPU.ShuffleOperations;
using System;
using System.Threading.Tasks;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        #region Reduction Helpers

        /// <summary>
        /// Computes the required number of temp-storage elements for the a reduction and the given data length.
        /// </summary>
        /// <param name="dataLength">The number of data elements to reduce.</param>
        /// <returns>The required number of temp-storage elements.</returns>
        [Obsolete("Use Accelerator.ComputeReductionTempStorageSize. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Index ComputeReductionTempStorageSize(Index dataLength)
        {
            return Accelerator.ComputeReductionTempStorageSize(dataLength);
        }

        #endregion

        #region Atomic Reduction

        /// <summary>
        /// Creates a new instance of an atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <returns>The created reduction handler.</returns>
        [Obsolete("Use Accelerator.CreateAtomicReduction. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public AtomicReduction<T, TShuffleDown, TReduction> CreateAtomicReduction<T, TShuffleDown, TReduction>()
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.CreateAtomicReduction<T, TShuffleDown, TReduction>();
        }

        /// <summary>
        /// Creates a new instance of an atomic reduction handler using the provided
        /// shuffle-down and reduction logics.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The created reduction handler.</returns>
        [Obsolete("Use Accelerator.CreateAtomicReduction. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public AtomicReduction<T> CreateAtomicReduction<T, TShuffleDown, TReduction>(
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.CreateAtomicReduction<T, TShuffleDown, TReduction>(shuffleDown, reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        [Obsolete("Use Accelerator.AtomicReduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void AtomicReduce<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            Accelerator.AtomicReduce(
                stream,
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        [Obsolete("Use Accelerator.AtomicReduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void AtomicReduce<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            Accelerator.AtomicReduce(
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.AtomicReduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public T AtomicReduce<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.AtomicReduce(
                stream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.AtomicReduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public T AtomicReduce<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.AtomicReduce(
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.AtomicReduceAsync. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Task<T> AtomicReduceAsync<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.AtomicReduceAsync(
                stream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.AtomicReduceAsync. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Task<T> AtomicReduceAsync<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Accelerator.AtomicReduceAsync(
                input,
                shuffleDown,
                reduction);
        }

        #endregion

        #region Reduction

        /// <summary>
        /// Creates a new instance of a atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <returns>The created reduction handler.</returns>
        [Obsolete("Use Accelerator.CreateReduction. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Reduction<T, TShuffleDown, TReduction> CreateReduction<T, TShuffleDown, TReduction>()
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.CreateReduction<T, TShuffleDown, TReduction>();
        }

        /// <summary>
        /// Creates a new instance of a atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The created reduction handler.</returns>
        [Obsolete("Use Accelerator.CreateReduction. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Reduction<T> CreateReduction<T, TShuffleDown, TReduction>(
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.CreateReduction<T, TShuffleDown, TReduction>(
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reduce<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            Accelerator.Reduce(
                stream,
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reduce<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            Accelerator.Reduce(
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="temp">The temporary view to store the temporary values.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reduce<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<T> temp,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            Accelerator.Reduce(
                stream,
                input,
                output,
                temp,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="temp">The temporary view to store the temporary values.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Reduce<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<T> temp,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            Accelerator.Reduce(
                input,
                output,
                temp,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public T Reduce<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.Reduce(
                stream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.Reduce. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public T Reduce<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.Reduce(
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.ReduceAsync. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Task<T> ReduceAsync<T, TShuffleDown, TReduction>(
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.ReduceAsync(
                stream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        [Obsolete("Use Accelerator.ReduceAsync. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Task<T> ReduceAsync<T, TShuffleDown, TReduction>(
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Accelerator.ReduceAsync(
                input,
                shuffleDown,
                reduction);
        }

        #endregion
    }
}
