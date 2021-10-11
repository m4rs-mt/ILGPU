﻿// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SimpleConstants
{
    class Program
    {
        /// <summary>
        /// A global constant.
        /// </summary>
        const int ConstantValue = 1;

        /// <summary>
        /// A readonly field.
        /// </summary>
        [SuppressMessage(
            "Performance",
            "CA1802:Use literals where appropriate",
            Justification = "Testing readonly value")]
        static readonly int ReadOnlyValue = 2;

        /// <summary>
        /// The default write-enabled value.
        /// </summary>
        const int DefaultWriteEnabledValue = 4;

        /// <summary>
        /// A write-enabled field.
        /// </summary>
        static int WriteEnabledValue = DefaultWriteEnabledValue;

        /// <summary>
        /// A simple 1D kernel. By default, all kernels can access global constants
        /// (since they will be inlined by the compiler by default).
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ConstantKernel(
            Index1D index,
            ArrayView<int> dataView)
        {
            dataView[index] = ConstantValue;
        }

        /// <summary>
        ///A simple 1D kernel. By default, all kernels can access static readonly
        /// fields (since they are immutable after their initialization).
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void StaticFieldAccessKernel(
            Index1D index,
            ArrayView<int> dataView)
        {
            dataView[index] = ReadOnlyValue;
        }

        /// <summary>
        /// A simple 1D kernel. All kernels reject read accesses to write-enabled
        /// static fields by default. However, you can disable this restriction by using the
        /// CompileUnitFlags.InlineMutableStaticFieldValues flag.
        ///
        /// Caution: the value of static field will be read at the time of compiling the 
        /// kernel and the resolved value will be inlined. Hence, changing the field
        /// value does not affect the value that was inlined into the compiled kernel.
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void StaticNonReadOnlyFieldAccessKernel(
            Index1D index,
            ArrayView<int> dataView)
        {
            dataView[index] = WriteEnabledValue;
        }

        /// <summary>
        /// A simple 1D kernel. All kernels reject write accesses to static
        /// fields by default. However, you can skip such assignments by using the
        /// CompileUnitFlags.IgnoreStaticFieldStores flag.
        ///
        /// Caution: only the store operations to the static fields will be ignored.
        /// This does not affect possible other side effects that might be caused
        /// during the computation of the "stored" value.
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void StaticFieldWriteAccessKernel(
            Index1D index,
            ArrayView<int> dataView)
        {
            WriteEnabledValue = index;
        }

        static void LaunchKernel(
            Accelerator accelerator,
            Action<Index1D, ArrayView<int>> method,
            int? expectedValue)
        {
            var kernel = accelerator.LoadAutoGroupedStreamKernel(method);
            using var buffer = accelerator.Allocate1D<int>(1024);
            kernel((int)buffer.Length, buffer.View);

            if (expectedValue.HasValue)
            {
                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Debug.Assert(data[i] == expectedValue);
            }
        }

        /// <summary>
        /// Demonstrates different use cases of constants and static fields.
        /// </summary>
        static void Main()
        {
            // All kernels reject read accesses to write-enabled static fields by default.
            // However, you can disable this restriction via:
            // StaticFields(StaticFieldMode.MutableStaticFields).

            // All kernels reject write accesses to static fields by default.
            // However, you can skip such assignments by via:
            // StaticFields(StaticFieldMode.IgnoreStaticFieldStores).

            // Create main context
            using var context = Context.Create(builder =>
                builder.Default()
                .StaticFields(StaticFieldMode.MutableStaticFields | StaticFieldMode.IgnoreStaticFieldStores));

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Launch ConstantKernel:
                LaunchKernel(
                    accelerator,
                    ConstantKernel,
                    ConstantValue);

                // Launch StaticFieldAccessKernel:
                LaunchKernel(
                    accelerator,
                    StaticFieldAccessKernel,
                    ReadOnlyValue);

                // Launch StaticNonReadOnlyFieldAccessKernel while inlining static field values:
                WriteEnabledValue = DefaultWriteEnabledValue;
                LaunchKernel(
                    accelerator,
                    StaticNonReadOnlyFieldAccessKernel,
                    DefaultWriteEnabledValue);
                // Note that a change of the field WriteEnabledValue will not change the result
                // of a previously compiled kernel that accessed the field WriteEnabledValue.

                // Launch StaticFieldWriteAccessKernel while ignoring static stores:
                // Note that the CPU accelerator will write to static field during execution!
                LaunchKernel(
                    accelerator,
                    StaticFieldWriteAccessKernel,
                    null);

                // Wait for the kernels to finish before the accelerator is disposed
                // at the end of this block.
                accelerator.Synchronize();
            }
        }
    }
}
