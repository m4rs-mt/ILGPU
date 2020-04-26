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
using ILGPU.Backends.OpenCL;
using ILGPU.Backends.PTX;
using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System;
using System.Reflection;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1801 // Review unused parameters

namespace CustomIntrinsics
{
    /// <summary>
    /// A class demonstrating a custom intrinsic implementation that relies
    /// on remapping its implementation to different versions.
    /// </summary>
    static class CustomRemappedIntrinsic
    {
        /// <summary>
        /// Mark a custom intrinsic function with the <see cref="IntrinsicImplementation"/>
        /// attribute such that ILGPU knows that it has to map this function to another implementation.
        /// </summary>
        /// <param name="x">Some value.</param>
        /// <returns>Some other value.</returns>
        [IntrinsicImplementation]
        public static int ComputeBackendDependent(int x)
        {
            // The following code will be used by the CPU (IL) backend
            return x;
        }

        /// <summary>
        /// The Cuda (PTX) implementation.
        /// </summary>
        public static int ComputePTX(int x) => 2 * x;

        /// <summary>
        /// The OpenCL implementation.
        /// </summary>
        public static int ComputeCL(int x) => 3 * x;

        /// <summary>
        /// Registers intrinsic implementations using remapping.
        /// </summary>
        public static void EnableDemoRemappingIntrinsic(this Context context)
        {
            var remappingType = typeof(CustomRemappedIntrinsic);
            var methodInfo = remappingType.GetMethod(
                nameof(ComputeBackendDependent),
                BindingFlags.Public | BindingFlags.Static);

            // Register the Cuda version (optional)
            context.IntrinsicManager.RegisterMethod(
                methodInfo,
                new PTXIntrinsic(
                    remappingType,
                    nameof(ComputePTX),
                    IntrinsicImplementationMode.Redirect));

            // Register the CL version (optional)
            context.IntrinsicManager.RegisterMethod(
                methodInfo,
                new CLIntrinsic(
                    remappingType,
                    nameof(ComputeCL),
                    IntrinsicImplementationMode.Redirect));
        }
    }

    /// <summary>
    /// A class demonstrating a custom intrinsic implementation that relies
    /// on custom code generators.
    /// </summary>
    static class CustomCodeGeneratorIntrinsic
    {
        /// <summary>
        /// Mark a custom intrinsic function with the <see cref="IntrinsicImplementation"/>
        /// attribute such that ILGPU knows that it has to use a custom code generator later on.
        /// </summary>
        /// <param name="x">Some value.</param>
        /// <returns>Some other value.</returns>
        [IntrinsicImplementation]
        public static int ComputeBackendDependentUsingCodeGenerator(int x)
        {
            // The following code will be used by the CPU (IL) backend
            return x;
        }

        /// <summary>
        /// The Cuda (PTX) implementation.
        /// </summary>
        /// <remarks>
        /// Note that this function signature corresponds to the PTX-backend specific
        /// delegate type <see cref="PTXIntrinsic.Handler"/>.
        /// </remarks>
        static void GeneratePTXCode(
            PTXBackend backend,
            PTXCodeGenerator codeGenerator,
            Value value)
        {
            // The passed value will be the call node in this case
            // Load X parameter register (first argument)
            var xRegister = codeGenerator.LoadPrimitive(value[0]);

            // Allocate target register to write our result to
            var target = codeGenerator.AllocateHardware(value);

            // Emit our desired instructions
            using (var command = codeGenerator.BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Mul,
                    ArithmeticBasicValueType.Int32,
                    false)))
            {
                command.AppendArgument(target);
                command.AppendArgument(xRegister);
                command.AppendConstant(2);
            }
        }

        /// <summary>
        /// The OpenCL implementation.
        /// </summary>
        /// <remarks>
        /// Note that this function signature corresponds to the OpenCL-backend specific
        /// delegate type <see cref="CLIntrinsic.Handler"/>.
        /// </remarks>
        static void GenerateCLCode(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
        {
            // The passed value will be the call node in this case
            // Load X parameter variable (first argument)
            var xVariable = codeGenerator.Load(value[0]);

            // Allocate target variable to write our result to
            var target = codeGenerator.Allocate(value);

            // Emit our desired instructions
            using (var statement = codeGenerator.BeginStatement(target))
            {
                statement.Append(xVariable);
                statement.AppendCommand(
                    CLInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Mul,
                        false,
                        out var _));
                statement.AppendConstant(2);
            }
        }

        /// <summary>
        /// Registers intrinsic implementations using remapping.
        /// </summary>
        public static void EnableDemoCodeGeneratorIntrinsic(this Context context)
        {
            var remappingType = typeof(CustomCodeGeneratorIntrinsic);
            var methodInfo = remappingType.GetMethod(
                nameof(ComputeBackendDependentUsingCodeGenerator),
                BindingFlags.Public | BindingFlags.Static);

            // Register the Cuda version (optional)
            context.IntrinsicManager.RegisterMethod(
                methodInfo,
                new PTXIntrinsic(
                    remappingType,
                    nameof(GeneratePTXCode),
                    IntrinsicImplementationMode.GenerateCode));

            // Register the CL version (optional)
            context.IntrinsicManager.RegisterMethod(
                methodInfo,
                new CLIntrinsic(
                    remappingType,
                    nameof(GenerateCLCode),
                    IntrinsicImplementationMode.GenerateCode));
        }
    }

    class Program
    {
        /// <summary>
        /// Demo kernel demonstrating intrinsic remapping.
        /// </summary>
        public static void KernelUsingCustomIntrinsic(Index1 index, ArrayView<int> view)
        {
            // Invoke the intrinsic like a default function
            view[index] = CustomRemappedIntrinsic.ComputeBackendDependent(index);
        }

        /// <summary>
        /// Demo kernel demonstrating intrinsic code generation.
        /// </summary>
        public static void KernelUsingCustomCodeGeneratorIntrinsic(Index1 index, ArrayView<int> view)
        {
            // Invoke the intrinsic like a default function
            view[index] = CustomCodeGeneratorIntrinsic.ComputeBackendDependentUsingCodeGenerator(index);
        }

        /// <summary>
        /// This sample demonstrates the implementation of custom intrinsic functions.
        /// </summary>
        static void Main()
        {
            // Create main context
            using (var context = new Context())
            {
                // Enable our custom intrinsics
                context.EnableDemoRemappingIntrinsic();
                context.EnableDemoCodeGeneratorIntrinsic();

                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        // Compile and load kernels using our custom intrinsic implementation
                        var remappingKernel = accelerator.LoadAutoGroupedStreamKernel<Index1, ArrayView<int>>(
                            KernelUsingCustomIntrinsic);
                        var codeGeneratorKernel = accelerator.LoadAutoGroupedStreamKernel<Index1, ArrayView<int>>(
                            KernelUsingCustomCodeGeneratorIntrinsic);

                        using (var buffer = accelerator.Allocate<int>(32))
                        {
                            remappingKernel(buffer.Length, buffer.View);
                            var data = buffer.GetAsArray();
                            Console.Write("Remapping: ");
                            Console.WriteLine(string.Join(", ", data));

                            codeGeneratorKernel(buffer.Length, buffer.View);
                            data = buffer.GetAsArray();
                            Console.Write("CodeGeneration: ");
                            Console.WriteLine(string.Join(", ", data));
                        }
                    }
                }
            }
        }
    }
}

#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CA1801 // Review unused parameters
