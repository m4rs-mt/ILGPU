// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: SequenceExtensions.cs (obsolete)
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        /// <summary>
        /// Creates a sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <returns>The loaded sequencer.</returns>
        [Obsolete("Use Accelerator.CreateSequencer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Sequencer<T, TSequencer> CreateSequencer<T, TSequencer>()
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            return Accelerator.CreateSequencer<T, TSequencer>();
        }

        /// <summary>
        /// Creates a batched sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <returns>The loaded sequencer.</returns>
        [Obsolete("Use Accelerator.CreateBatchedSequencer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public BatchedSequencer<T, TSequencer> CreateBatchedSequencer<T, TSequencer>()
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            return Accelerator.CreateBatchedSequencer<T, TSequencer>();
        }

        /// <summary>
        /// Creates a repeated sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <returns>The loaded sequencer.</returns>
        [Obsolete("Use Accelerator.CreateRepeatedSequencer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public BatchedSequencer<T, TSequencer> CreateRepeatedSequencer<T, TSequencer>()
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            return Accelerator.CreateRepeatedSequencer<T, TSequencer>();
        }

        /// <summary>
        /// Creates a repeated batched sequencer that is defined by the given element type and the type of the sequencer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <returns>The loaded sequencer.</returns>
        [Obsolete("Use Accelerator.CreateRepeatedBatchedSequencer. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public RepeatedBatchedSequencer<T, TSequencer> CreateRepeatedBatchedSequencer<T, TSequencer>()
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            return Accelerator.CreateRepeatedBatchedSequencer<T, TSequencer>();
        }

        /// <summary>
        /// Computes a new sequence of values from 0 to view.Length - 1 and writes
        /// the computed values to the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.Sequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Sequence<T, TSequencer>(
            AcceleratorStream stream,
            ArrayView<T> view,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.Sequence(stream, view, sequencer);
        }

        /// <summary>
        /// Computes a new sequence of values from 0 to view.Length - 1 and writes
        /// the computed values to the given view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="view">The target view.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.Sequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void Sequence<T, TSequencer>(
            ArrayView<T> view,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.Sequence(view, sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to sequenceLength, ... and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = [0, sequenceLength]
        /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.RepeatedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void RepeatedSequence<T, TSequencer>(
            AcceleratorStream stream,
            ArrayView<T> view,
            Index sequenceLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.RepeatedSequence(
                stream,
                view,
                sequenceLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence of values from 0 to sequenceLength, from 0 to sequenceLength, ... and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = [0, sequenceLength]
        /// - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.RepeatedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void RepeatedSequence<T, TSequencer>(
            ArrayView<T> view,
            Index sequenceLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.RepeatedSequence(
                view,
                sequenceLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceBatchLength - 1] = 0,,
        /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.BatchedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void BatchedSequence<T, TSequencer>(
            AcceleratorStream stream,
            ArrayView<T> view,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.BatchedSequence(
                stream,
                view,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new sequence of batched values of length sequenceBatchLength, and writes
        /// the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceBatchLength - 1] = 0,,
        /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.BatchedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void BatchedSequence<T, TSequencer>(
            ArrayView<T> view,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.BatchedSequence(
                view,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values (of length sequenceBatchLength),
        /// and writes the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.RepeatedBatchedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void RepeatedBatchedSequence<T, TSequencer>(
            AcceleratorStream stream,
            ArrayView<T> view,
            Index sequenceLength,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.RepeatedBatchedSequence(
                stream,
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);
        }

        /// <summary>
        /// Computes a new repeated sequence (of length sequenceLength) of batched values (of length sequenceBatchLength),
        /// and writes the computed values to the given view. Afterwards, the target view will contain the following values:
        /// - [0, sequenceLength - 1] = 
        ///       - [0, sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - [sequenceLength, sequenceLength * 2 - 1]
        ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
        ///       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
        ///       - ...
        /// - ...
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TSequencer">The type of the sequencer to use.</typeparam>
        /// <param name="view">The target view.</param>
        /// <param name="sequenceLength">The length of a single sequence.</param>
        /// <param name="sequenceBatchLength">The length of a single batch.</param>
        /// <param name="sequencer">The used sequencer.</param>
        [Obsolete("Use Accelerator.RepeatedBatchedSequence. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void RepeatedBatchedSequence<T, TSequencer>(
            ArrayView<T> view,
            Index sequenceLength,
            Index sequenceBatchLength,
            TSequencer sequencer)
            where T : struct
            where TSequencer : struct, ISequencer<T>
        {
            Accelerator.RepeatedBatchedSequence(
                view,
                sequenceLength,
                sequenceBatchLength,
                sequencer);
        }
    }
}
