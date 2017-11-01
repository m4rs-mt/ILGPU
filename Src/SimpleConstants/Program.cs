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
using System.Diagnostics;
using System.Reflection;

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
        static readonly int ReadOnlyValue = 2;

        /// <summary>
        /// A write-enabled field.
        /// </summary>
        static int WriteEnabledValue = 4;

        /// <summary>
        /// A simple 1D kernel. By default, all kernels can access global constants
        /// (since they will be inlined by the compiler by default).
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ConstantKernel(
            Index index,
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
            Index index,
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
            Index index,
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
            Index index,
            ArrayView<int> dataView)
        {
            WriteEnabledValue = index;
        }

        static void LaunchKernel(
            Accelerator accelerator,
            Action<Index, ArrayView<int>> method,
            int? expectedValue)
        {
            var kernel = accelerator.LoadAutoGroupedStreamKernel(method);
            using (var buffer = accelerator.Allocate<int>(1024))
            {
                kernel(buffer.Length, buffer.View);

                // Wait for the kernel to finish...
                accelerator.Synchronize();

                if (expectedValue.HasValue)
                {
                    var data = buffer.GetAsArray();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Debug.Assert(data[i] == expectedValue);
                }
            }
        }

        /// <summary>
        /// Demonstates different use cases of constants and static fields.
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
                    using (var accelerator = Accelerator.Create(context, acceleratorId, CompileUnitFlags.None))
                    {
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

                        // Launch StaticNonReadOnlyFieldAccessKernel:
                        try
                        {
                            LaunchKernel(
                                accelerator,
                                StaticNonReadOnlyFieldAccessKernel,
                                WriteEnabledValue);
                        }
                        catch (NotSupportedException)
                        {
                            // All kernels reject read accesses to write-enabled static fields by default.
                            // However, you can disable this restriction via:
                            // CompileUnitFlags.InlineMutableStaticFieldValues.
                            Console.WriteLine("Rejected reading a write-enabled static field");
                        }

                        // Launch StaticFieldWriteAccessKernel:
                        try
                        {
                            LaunchKernel(
                                accelerator,
                                StaticFieldWriteAccessKernel,
                                WriteEnabledValue);
                        }
                        catch (NotSupportedException)
                        {
                            // All kernels reject write accesses to static fields by default.
                            // However, you can skip such assignments by via:
                            // CompileUnitFlags.IgnoreStaticFieldStores.
                            Console.WriteLine("Rejected write to static field");
                        }
                    }

                    using (var accelerator = Accelerator.Create(context, acceleratorId, CompileUnitFlags.InlineMutableStaticFieldValues))
                    {
                        // Launch StaticNonReadOnlyFieldAccessKernel while inlining static field values:
                        LaunchKernel(
                            accelerator,
                            StaticNonReadOnlyFieldAccessKernel,
                            WriteEnabledValue);
                        // Note that a change of the field WriteEnabledValue will not change the result
                        // of a previously compiled kernel that accessed the field WriteEnabledValue.
                    }

                    using (var accelerator = Accelerator.Create(context, acceleratorId, CompileUnitFlags.IgnoreStaticFieldStores))
                    {
                        // Launch StaticFieldWriteAccessKernel while ignoring static stores:
                        LaunchKernel(
                            accelerator,
                            StaticFieldWriteAccessKernel,
                            null);
                    }
                }
            }
        }
    }
}
