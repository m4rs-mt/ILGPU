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
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Diagnostics;

namespace SimpleViews
{
    class Program
    {
        static void UnsafeAccess(ArrayView<int> view)
        {
            // Cast int view to a double view.
            // Note that the length of the view is adapted accordingly.
            var doubleView = view.Cast<double>();
            Debug.Assert(doubleView.Length * sizeof(double) == view.Length * sizeof(int));
            doubleView[0] = double.NaN;

            // Unsafe Bitcast int view to a byte view.
            // Note that the length of the view is adapted accordingly.
            var byteView = view.Cast<byte>();
            Debug.Assert(byteView.Length * sizeof(byte) == view.Length * sizeof(int));
            for (int i = 0; i < sizeof(double); ++i)
                Console.WriteLine($"DoubleAsByte[{i}] = {byteView[i]}");
        }

        static void SubViewAccess(ArrayView<int> view)
        {
            // Creates a sub view that represents a view from index 10 to view.Length - 1
            var subView = view.SubView(10);
            Debug.Assert(subView.Length == view.Length - 10);

            // Write value to sub view and output value
            subView[0] = 42;
            Console.WriteLine($"Value of sub view at index 0: {subView[0]} = value of view at index 10: {view[10]}");

            // Creates a sub view that represents a view from index 10 with length 20
            // (up to index 10 + 20)
            var subView2 = view.SubView(10, 20);
            Debug.Assert(subView2.Length == 20);
            Console.WriteLine($"Value of sub view 2 at index 0: {subView2[0]} = value of view at index 10: {view[10]}");

            // Creates a sub view that represents a view from index 20 with length 2
            var subView3 = subView2.SubView(10, 2);
            subView3[1] = 23;

            // An access of the form subView3[2] will trigger an OutOfBounds assertion
            Debug.Assert(subView3.Length == 2);
            Console.WriteLine($"Value of sub view 3 at index 1: {subView3[1]} = value of view at index 21: {view[21]}");
        }

        static void VariableViewAccess(ArrayView<int> view)
        {
            // Creates a variable view that points to the last accessible element
            // of the provided view
            var variableView = view.VariableView(view.Length - 1);
            variableView.Value = 13;

            Debug.Assert(variableView.Value == view[view.Length - 1]);
        }

        static void UnsafeVariableViewAccess(ArrayView<int> view)
        {
            // Creates a variable view that points to the first element
            var variableView = view.VariableView(0);

            // Cast variable view to an accessible sub element in range.
            // The passed offset is the relative offset of the sub view in bytes.
            // The primary use case for this functionality are direct accesses
            // to structure members.
            var subView = variableView.SubView<short>(sizeof(short));
            subView.Value = short.MaxValue;

            Debug.Assert(variableView.Value == short.MaxValue << 16);
        }

        /// <summary>
        /// Demonstrates the use of array views. Operations on array views are
        /// supported on all accelerators.
        /// </summary>
        static void Main()
        {
            // Create main context
            using (var context = Context.Create(builder => builder.DefaultCPU()))
            {
                // We perform all operations in CPU memory here
                using (var accelerator = context.CreateCPUAccelerator(0))
                {
                    using (var buffer = accelerator.Allocate1D<int>(1024))
                    {
                        // Retrieve a view to the whole buffer.
                        ArrayView<int> bufferView = buffer.View;

                        // Note that accessing an array view which points to memory
                        // that is not accessible in the current context triggers
                        // an invalid access exception.
                        // For instance, array views that point to CUDA memory are 
                        // inaccessible from the CPU by default (and vice-versa).
                        // We can ignore this restriction in the current context since we
                        // perform all operations in CPU memory.

                        // Perform some unsafe operations on array views.
                        UnsafeAccess(bufferView);

                        // SubView access
                        SubViewAccess(bufferView);

                        // VariableView access
                        VariableViewAccess(bufferView);

                        // Perform some unsafe operations on variable views.
                        UnsafeVariableViewAccess(bufferView);
                    }
                }
            }
        }
    }
}
