// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Lightning;
using ILGPU.Lightning.Sequencers;
using System;

namespace LightningSequence
{
    /// <summary>
    /// A custom structure that can be used in a memory buffer.
    /// </summary>
    struct CustomStruct
    {
        public Index First;
        public Index Second;

        public override string ToString()
        {
            return $"First: {First}, Second: {Second}";
        }
    }

    /// <summary>
    /// A custom sequencer of <see cref="CustomStruct"/> elements.
    /// </summary>
    struct CustomSequencer : ISequencer<CustomStruct>
    {
        /// <summary>
        /// Constructs a new custom sequencer.
        /// </summary>
        /// <param name="secondOffset">The base offset for the second element.</param>
        public CustomSequencer(Index secondOffset)
        {
            SecondOffset = secondOffset;
        }

        /// <summary>
        /// Returns the base offset for the second element.
        /// </summary>
        public Index SecondOffset { get; }

        /// <summary>
        /// Computes the sequence element for the corresponding <paramref name="sequenceIndex"/>.
        /// </summary>
        /// <param name="sequenceIndex">The sequence index for the computation of the corresponding value.</param>
        /// <returns>The computed sequence value.</returns>
        public CustomStruct ComputeSequenceElement(Index sequenceIndex)
        {
            return new CustomStruct()
            {
                First = sequenceIndex,
                Second = SecondOffset + sequenceIndex
            };
        }
    }

    class Program
    {
        /// <summary>
        /// Demonstrates the sequence functionality.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void Sequence(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length / 2 - 1).
                // Note that in this case, the sequencer uses the default accelerator stream.
                lc.Sequence(buffer.View.GetSubView(0, buffer.Length / 2), new Int32Sequencer());

                // Creates a sequence (from 0 to buffer.Length / 2 - 1).
                // Note that this overload requires an explicit accelerator stream.
                lc.Sequence(lc.DefaultStream, buffer.View.GetSubView(buffer.Length / 2), new Int32Sequencer());

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");
            }

            // Custom sequencer
            using (var buffer = lc.Allocate<CustomStruct>(64))
            {
                lc.Sequence(buffer.View, new CustomSequencer(32));

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"CustomData[{i}] = {data[i]}");
            }

            // Calling the convenient Sequence function on the lightning context
            // involves internal heap allocations. This can be avoided by constructing
            // a sequencer explicitly:
            var sequencer = lc.CreateSequencer<CustomStruct, CustomSequencer>();
            using (var buffer = lc.Allocate<CustomStruct>(64))
            {
                sequencer(
                    lc.DefaultStream,
                    buffer.View,
                    new CustomSequencer(64));

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"CustomDataSpecialized[{i}] = {data[i]}");
            }
        }

        /// <summary>
        /// Demonstrates the repeated-sequence functionality.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void RepeatedSequence(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length / 2 - 1):
                // - [0, sequenceLength - 1] = [0, sequenceLength]
                // - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
                // Note that the sequencer uses the default accelerator stream in this case.
                lc.RepeatedSequence(
                    buffer.View.GetSubView(0, buffer.Length / 2),
                    2
                    , new Int32Sequencer());

                // Creates a sequence (from 0 to buffer.Length / 2 - 1).
                // Note that this overload requires an explicit accelerator stream.
                lc.RepeatedSequence(
                    lc.DefaultStream,
                    buffer.View.GetSubView(buffer.Length / 2),
                    4,
                    new Int32Sequencer());

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"RepeatedData[{i}] = {data[i]}");
            }

            // There is also a CreateRepeatedSequencer function that avoids
            // unnecessary boxing.
        }

        /// <summary>
        /// Demonstrates the batched-sequence functionality.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void BatchedSequence(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length / 2 - 1):
                // - [0, sequenceBatchLength - 1] = 0,,
                // - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
                // Note that in this case, the sequencer uses the default accelerator stream.
                lc.BatchedSequence(
                    buffer.View.GetSubView(0, buffer.Length / 2),
                    2
                    , new Int32Sequencer());

                // Creates a sequence (from 0 to buffer.Length / 2 - 1).
                // Note that this overload requires an explicit accelerator stream.
                lc.BatchedSequence(
                    lc.DefaultStream,
                    buffer.View.GetSubView(buffer.Length / 2),
                    4,
                    new Int32Sequencer());

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"BatchedData[{i}] = {data[i]}");
            }
        }

        /// <summary>
        /// Demonstrates the repeated-batched-sequence functionality.
        /// </summary>
        /// <param name="lc">The target lightning context.</param>
        static void RepeatedBatchedSequence(LightningContext lc)
        {
            using (var buffer = lc.Allocate<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length / 2 - 1):
                // - [0, sequenceLength - 1] = 
                //       - [0, sequenceBatchLength - 1] = sequencer(0),
                //       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
                //       - ...
                // - [sequenceLength, sequenceLength * 2 - 1]
                //       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
                //       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
                //       - ...
                // Note that the sequencer uses the default accelerator stream in this case.
                lc.RepeatedBatchedSequence(
                    buffer.View.GetSubView(0, buffer.Length / 2),
                    2,
                    4,
                    new Int32Sequencer());

                // Creates a sequence (from 0 to buffer.Length / 2 - 1).
                // Note that this overload requires an explicit accelerator stream.
                lc.RepeatedBatchedSequence(
                    lc.DefaultStream,
                    buffer.View.GetSubView(buffer.Length / 2),
                    6,
                    8,
                    new Int32Sequencer());

                lc.Synchronize();

                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"RepeatedBatchedData[{i}] = {data[i]}");
            }
        }

        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in LightningContext.Accelerators)
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var lc = LightningContext.CreateContext(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {lc}");

                        Sequence(lc);
                        RepeatedSequence(lc);
                        BatchedSequence(lc);
                        RepeatedBatchedSequence(lc);
                    }
                }
            }
        }
    }
}
