// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Algorithms.Sequencers;
using ILGPU.Runtime;
using System;

namespace AlgorithmsSequence
{
    /// <summary>
    /// A custom structure that can be used in a memory buffer.
    /// </summary>
    struct CustomStruct
    {
        public LongIndex1D First;
        public LongIndex1D Second;

        public override string ToString() =>
            $"First: {First}, Second: {Second}";
    }

    /// <summary>
    /// A custom sequencer of <see cref="CustomStruct"/> elements.
    /// </summary>
    readonly struct CustomSequencer : ISequencer<CustomStruct>
    {
        /// <summary>
        /// Constructs a new custom sequencer.
        /// </summary>
        /// <param name="secondOffset">The base offset for the second element.</param>
        public CustomSequencer(LongIndex1D secondOffset)
        {
            SecondOffset = secondOffset;
        }

        /// <summary>
        /// Returns the base offset for the second element.
        /// </summary>
        public LongIndex1D SecondOffset { get; }

        /// <summary>
        /// Computes the sequence element for the corresponding <paramref name="sequenceIndex"/>.
        /// </summary>
        /// <param name="sequenceIndex">The sequence index for the computation of the corresponding value.</param>
        /// <returns>The computed sequence value.</returns>
        public CustomStruct ComputeSequenceElement(LongIndex1D sequenceIndex) =>
            new CustomStruct()
            {
                First = sequenceIndex,
                Second = SecondOffset + sequenceIndex
            };
    }

    class Program
    {
        /// <summary>
        /// Demonstrates the sequence functionality.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void Sequence(Accelerator accelerator)
        {
            using (var buffer = accelerator.Allocate1D<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length - 1).
                accelerator.Sequence(accelerator.DefaultStream, buffer.View, new Int32Sequencer());

                accelerator.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");
            }

            // Custom sequencer
            using (var buffer = accelerator.Allocate1D<CustomStruct>(64))
            {
                accelerator.Sequence(accelerator.DefaultStream, buffer.View, new CustomSequencer(32));

                accelerator.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"CustomData[{i}] = {data[i]}");
            }

            // Calling the convenient Sequence function on the accelerator
            // involves internal heap allocations. This can be avoided by constructing
            // a sequencer explicitly:
            var sequencer = accelerator.CreateSequencer<CustomStruct, Stride1D.Dense, CustomSequencer>();
            using (var buffer = accelerator.Allocate1D<CustomStruct>(64))
            {
                sequencer(
                    accelerator.DefaultStream,
                    buffer.View,
                    new CustomSequencer(64));

                accelerator.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"CustomDataSpecialized[{i}] = {data[i]}");
            }
        }

        /// <summary>
        /// Demonstrates the repeated-sequence functionality.
        /// </summary>
        /// <param name="accl">The target accelerator.</param>
        static void RepeatedSequence(Accelerator accl)
        {
            using (var buffer = accl.Allocate1D<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length - 1):
                // - [0, sequenceLength - 1] = [0, sequenceLength]
                // - [sequenceLength, sequenceLength * 2 -1] = [0, sequenceLength]
                accl.RepeatedSequence(
                    accl.DefaultStream,
                    buffer.View.SubView(0, buffer.Length),
                    2,
                    new Int32Sequencer());

                accl.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"RepeatedData[{i}] = {data[i]}");
            }

            // There is also a CreateRepeatedSequencer function that avoids
            // unnecessary heap allocations.
        }

        /// <summary>
        /// Demonstrates the batched-sequence functionality.
        /// </summary>
        /// <param name="accl">The target accelerator.</param>
        static void BatchedSequence(Accelerator accl)
        {
            using (var buffer = accl.Allocate1D<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length):
                // - [0, sequenceBatchLength - 1] = 0,
                // - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
                accl.BatchedSequence(
                    accl.DefaultStream,
                    buffer.View,
                    2,
                    new Int32Sequencer());

                accl.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"BatchedData[{i}] = {data[i]}");

                // There is also a CreateBatchedSequencer function that avoids
                // unnecessary heap allocations.
            }
        }

        /// <summary>
        /// Demonstrates the repeated-batched-sequence functionality.
        /// </summary>
        /// <param name="accl">The target accelerator.</param>
        static void RepeatedBatchedSequence(Accelerator accl)
        {
            using (var buffer = accl.Allocate1D<int>(64))
            {
                // Creates a sequence (from 0 to buffer.Length):
                // - [0, sequenceLength - 1] = 
                //       - [0, sequenceBatchLength - 1] = sequencer(0),
                //       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
                //       - ...
                // - [sequenceLength, sequenceLength * 2 - 1]
                //       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
                //       - [sequenceLength + sequenceBatchLength, sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
                //       - ...
                accl.RepeatedBatchedSequence(
                    accl.DefaultStream,
                    buffer.View,
                    2,
                    4,
                    new Int32Sequencer());

                accl.Synchronize();

                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"RepeatedBatchedData[{i}] = {data[i]}");

                // There is also a CreateRepeatedBatchedSequencer function that avoids
                // unnecessary heap allocations.
            }
        }

        static void Main()
        {
            using (var context = Context.CreateDefault())
            {
                // For each available accelerator...
                foreach (var device in context)
                {
                    using (var accelerator = device.CreateAccelerator(context))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        Sequence(accelerator);
                        RepeatedSequence(accelerator);
                        BatchedSequence(accelerator);
                        RepeatedBatchedSequence(accelerator);
                    }
                }
            }
        }
    }
}
