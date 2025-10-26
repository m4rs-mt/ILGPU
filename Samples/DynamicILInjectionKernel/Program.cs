// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1031
using ILGPU;
using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Frontend;
using ILGPU.IR;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace SimpleKernel
{
    class Program
    {
        private static float[] GetInputs() =>
            Enumerable.Range(0, 1024).Select(i => MathF.Tau * i / 1024f).ToArray();

        /// <summary>
        /// It's a simple method that calls the sine function but contains
        /// internal branching for generic specialization. Because the method
        /// uses the <see cref="System.Type"/> type and <c>throw</c> instructions,
        /// it cannot be compiled by ILGPU's normal compilation process.
        /// </summary>
        /// <typeparam name="T">The type of input and output.</typeparam>
        /// <param name="x">The input number.</param>
        /// <returns>Sine of <paramref name="x"/>.</returns>
        /// <exception cref="NotSupportedException"></exception>
        static T GenericSin<T>(T x)
            where T : unmanaged
        {
            if (typeof(T) == typeof(float))
                return Unsafe.BitCast<float, T>(MathF.Sin(Unsafe.BitCast<T, float>(x)));
            else if (typeof(T) == typeof(double))
                return Unsafe.BitCast<double, T>(Math.Cos(Unsafe.BitCast<T, double>(x)));
            else
                throw new NotSupportedException();
        }

        /// <summary>
        /// The first case that uses <see cref="GenericSin{T}(T)"/>. The method
        /// itself is not generic, but it internally references the type-
        /// parameterized <see cref="GenericSin{float}(float)"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="dataView"></param>
        static void MyKernel1(
            Index1D index,
            ArrayView<float> dataView)
        {
            dataView[index] = GenericSin(dataView[index]);
        }

        /// <summary>
        /// The another case that uses <see cref="GenericSin{T}(T)"/>. The
        /// method itself is generic, and it binds <see cref="GenericSin{T}(T)"/>
        /// when this method is called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="index"></param>
        /// <param name="dataView"></param>
        static void MyKernel2<T>(
            Index1D index,
            ArrayView<T> dataView)
            where T : unmanaged
        {
            dataView[index] = GenericSin(dataView[index]);
        }

        /// <summary>
        /// Experiments invoking <see cref="MyKernel1(Index1D, ArrayView{float})"/> as ILGPU compiled kernel.
        /// </summary>
        /// <param name="accelerator"></param>
        static void TestInvokeMyKernel1(Accelerator accelerator)
        {
            try
            {
                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(MyKernel1);
                using var buffer = accelerator.Allocate1D<float>(1024);
                buffer.CopyFromCPU(GetInputs());
                kernel((int)buffer.Length, buffer.View);
                var data1 = buffer.GetAsArray1D();
                Console.WriteLine($"[{string.Join(", ", data1.Take(10))}, ...]");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"An exception occurred: {exc.Message}");
            }
        }

        /// <summary>
        /// Experiments invoking <see cref="MyKernel2{float}(Index1D, ArrayView{float})"/> as ILGPU compiled kernel.
        /// </summary>
        /// <param name="accelerator"></param>
        static void TestInvokeMyKernel2(Accelerator accelerator)
        {
            try
            {
                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(MyKernel2);
                using var buffer = accelerator.Allocate1D<float>(1024);
                buffer.CopyFromCPU(GetInputs());
                kernel((int)buffer.Length, buffer.View);
                var data1 = buffer.GetAsArray1D();
                Console.WriteLine($"[{string.Join(", ", data1.Take(10))}, ...]");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"An exception occurred: {exc.Message}");
            }
        }


        /// <summary>
        /// Launches a simple 1D kernel.
        /// </summary>
        static void Main()
        {
            // Due to using `GenericSin<T>`, this operation will be failed.
            Console.WriteLine("<<< without dynamic IL injection for `GenericSin<float>` >>>");
            using (var context = Context.CreateDefault())
            {
                using var accelerator = context
                    .GetPreferredDevice(false)
                    .CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                Console.Write("Results from MyKernel1: ");
                TestInvokeMyKernel1(accelerator);
            }
            Console.WriteLine();

            // Since `GenericSin<float>` has been replaced with IL that contains no compilation errors,
            // `MyKernel1` and `MyKernel2` can be compiled and executed without errors.
            Console.WriteLine("<<< with dynamic IL injection for `GenericSin<float>` >>>");
            using (var context = Context.CreateDefault())
            {
                using var accelerator = context
                    .GetPreferredDevice(false)
                    .CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Injects a dynamic IL implementation for `GenericSin<float>`.
                // When the type parameter `T` is instantiated as `float`, the conditional branches
                // can be removed and the method can be optimized to a direct call to `MathF.Sin(float)`.
                // Therefore, we inject the precomputed optimized IL here.
                using (var codeGenerationPhase = accelerator.GetBackend().Context.BeginCodeGeneration())
                {
                    var methodInfoGenericSin = typeof(Program).GetMethod(nameof(GenericSin), BindingFlags.NonPublic | BindingFlags.Static);
                    var genericSinEPDesc = EntryPointDescription.FromExplicitlyGroupedKernel(methodInfoGenericSin.MakeGenericMethod(typeof(float)));
                    var mainContext = codeGenerationPhase.IRContext;

                    CodeGenerationResult generationResult;
                    using (var frontendPhase = codeGenerationPhase.BeginFrontendCodeGeneration())
                    {
                        var methodInfoMathFSin = typeof(MathF).GetMethod(nameof(MathF.Sin), BindingFlags.Public | BindingFlags.Static);
                        var disassembledMethod = new DisassembledMethod(
                            genericSinEPDesc.MethodSource,
                            ImmutableArray.Create<ILInstruction>(
                                new ILInstruction(0, ILInstructionType.Ldarg, default, popCount: 0, pushCount: 1, argument: 0, Location.Unknown),
                                new ILInstruction(1, ILInstructionType.Call, default, popCount: 1, pushCount: 1, argument: methodInfoMathFSin, Location.Unknown),
                                new ILInstruction(6, ILInstructionType.Ret, default, popCount: 1, pushCount: 0, argument: OpCodes.Ret, Location.Unknown)
                            ),
                            8);
                        generationResult = frontendPhase.GenerateCode(genericSinEPDesc.MethodSource, disassembledMethod);
                    }

                    if (codeGenerationPhase.IsFaulted)
                    {
                        throw codeGenerationPhase.LastException!;
                    }

                    var result = generationResult;
                    if (!result.HasResult)
                    {
                        throw new InvalidOperationException();
                    }
                    codeGenerationPhase.Optimize();
                }

                Console.WriteLine("<<< after dynamic IL injection for `GenericSin<float>` >>>");
                Console.Write("Results from MyKernel1: ");
                TestInvokeMyKernel1(accelerator);

                Console.Write("Results from MyKernel2: ");
                TestInvokeMyKernel2(accelerator);
            }
        }
    }
}
