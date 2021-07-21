// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2020 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace SpecializedKernel
{
    class Program
    {
        // A simple kernel with a specialized kernel parameter
        public static void SpecializedKernel(
            ArrayView<int> view,
            // This parameter will be replaced by a 'constant' value during
            // the first kernel call
            SpecializedValue<int> specialized)
        {
            var globalIndex = Grid.GlobalIndex.X;
            view[globalIndex] = specialized;
        }

        /// <summary>
        ///  A user-defined structure that can be used for specialization purposes.
        /// </summary>
        public readonly struct CustomStruct : IEquatable<CustomStruct>
        {
            public CustomStruct(int value1, int value2)
            {
                Value1 = value1;
                Value2 = value2;
            }

            public int Value1 { get; }

            public int Value2 { get; }

            // The following overwrites are required to find specialized kernels in the
            // specialization caches

            public bool Equals(CustomStruct other) =>
                Value1 == other.Value1 && Value2 == other.Value2;

            public override bool Equals(object obj) =>
                obj is CustomStruct other && Equals(other);

            public override int GetHashCode() =>
                Value1.GetHashCode() ^ Value2.GetHashCode();
        }

        // The specialization functionality supports user-defined types, as long as they
        // are value types and implement the IEquatable interface (and have useful
        // GetHashCode and Equals implementations).
        public static void SpecializedCustomStructKernel(
            ArrayView<int> view,
            SpecializedValue<CustomStruct> specialized)
        {
            var globalIndex = Grid.GlobalIndex.X;

            // Note that an implicit conversion from a specialized value to
            // a non-specialized value is possible. But: not the other way around ;)
            CustomStruct customValue = specialized;

            // The value is specialized and the additional optimization passes will
            // perform constant propagation to create an 'optimized' store with a single constant
            // value (in this case)
            view[globalIndex] = customValue.Value1 + customValue.Value2;
        }

        // The specialization also works with generic kernels
        static void SpecializedGenericKernel<TValue>(
            ArrayView<TValue> view,
            SpecializedValue<TValue> specialized)
            where TValue : unmanaged, IEquatable<TValue>
        {
            var globalIndex = Grid.GlobalIndex.X;
            view[globalIndex] = specialized;
        }

        static void Main()
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        int groupSize = accelerator.MaxNumThreadsPerGroup;

                        // Scenario 1: simple version
                        using (var buffer = accelerator.Allocate<int>(groupSize))
                        {
                            var kernel = accelerator.LoadStreamKernel<
                                ArrayView<int>,
                                SpecializedValue<int>>(SpecializedKernel);
                            kernel((1, groupSize), buffer.View, SpecializedValue.New(2));
                            kernel((1, groupSize), buffer.View, SpecializedValue.New(23));
                            kernel((1, groupSize), buffer.View, SpecializedValue.New(42));
                        }

                        // Scenario 2: custom structure
                        using (var buffer = accelerator.Allocate<int>(groupSize))
                        {
                            var kernel = accelerator.LoadStreamKernel<
                                ArrayView<int>,
                                SpecializedValue<CustomStruct>>(SpecializedCustomStructKernel);
                            kernel(
                                (1, groupSize),
                                buffer.View,
                                SpecializedValue.New(
                                    new CustomStruct(1, 7)));
                            kernel(
                                (1, groupSize),
                                buffer.View,
                                SpecializedValue.New(
                                    new CustomStruct(23, 42)));
                        }

                        // Scenario 3: generic kernel
                        using (var buffer = accelerator.Allocate<long>(groupSize))
                        {
                            var kernel = accelerator.LoadStreamKernel<
                                ArrayView<long>,
                                SpecializedValue<long>>(SpecializedGenericKernel);
                            kernel((1, groupSize), buffer.View, SpecializedValue.New(23L));
                            kernel((1, groupSize), buffer.View, SpecializedValue.New(42L));
                        }
                    }
                }
            }
        }
    }
}
