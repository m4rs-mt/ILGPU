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
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace AdvancedViews
{
    /// <summary>
    /// This structure holds several elements elements and our desired
    /// encapsulated element counter field.
    /// </summary>
    struct ComposedStructure
    {
        /// <summary>
        /// Stores the offset of the element-counter field in bytes.
        /// </summary>
        public static readonly int ElementCounterOffset =
            Interop.OffsetOf<ComposedStructure>(nameof(ElementCounter));

        public short SomeElement;
        public byte SomeOtherElement;
        public int ElementCounter;

        public ComposedStructure(
            short someElement,
            byte someOtherElement,
            int elementCounter)
        {
            SomeElement = someElement;
            SomeOtherElement = someOtherElement;
            ElementCounter = elementCounter;
        }
    }

    class Program
    {
        /// <summary>
        /// A simple kernel that uses a variable-sub-view access to compute
        /// the target memory location for an atomic operation.
        /// </summary>
        /// <param name="index">The thread index.</param>
        /// <param name="elements">The elements to check.</param>
        /// <param name="view"></param>
        /// <param name="comparisonValue"></param>
        static void MyKernel(
            Index index,
            ArrayView<int> elements,
            ArrayView<ComposedStructure> view,
            int comparisonValue)
        {
            var element = elements[index];
            if (element == comparisonValue)
            {
                var baseView = view.GetVariableView(0);
                var counterView = baseView.GetSubView<int>(ComposedStructure.ElementCounterOffset);
                Atomic.Add(counterView, 1);
            }
        }

        /// <summary>
        /// Demonstates the use of variable-sub-view accesses.
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        using (var loader = new SimpleKernel.SampleKernelLoader())
                        {
                            loader.CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                kernel =>
                                {
                                    var elementsBuffer = accelerator.Allocate<int>(1024);
                                    var composedStructBuffer = accelerator.Allocate<ComposedStructure>(1);
                                    elementsBuffer.MemSetToZero();
                                    composedStructBuffer.MemSetToZero();

                                    kernel.Launch(elementsBuffer.Length, elementsBuffer.View, composedStructBuffer.View, 0);

                                    accelerator.Synchronize();

                                    var composedResult = composedStructBuffer[0];
                                    Console.WriteLine("Composed.SomeElement = " + composedResult.SomeElement);
                                    Console.WriteLine("Composed.SomeOtherElement = " + composedResult.SomeOtherElement);
                                    Console.WriteLine("Composed.ElementCounter = " + composedResult.ElementCounter);

                                    composedStructBuffer.Dispose();
                                    elementsBuffer.Dispose();
                                });
                        }
                    }
                }
            }
        }
    }
}
