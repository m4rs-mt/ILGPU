﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LanguageTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class LanguageTests : TestBase
    {
        protected LanguageTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }


        internal static void PlainEmitKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("membar.gl;");
            }
            buffer[index] = index;
        }

        [Fact]
        [KernelMethod(nameof(PlainEmitKernel))]
        public void PlainEmit()
        {
            const int Length = 64;
            var expected = Enumerable.Range(0, Length).ToArray();

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }

        internal static void OutputEmitKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("add.s32 %0, %1, %2;", out int result, index.X, 42);
                buffer[index] = result;
            }
            else
            {
                buffer[index] = index;
            }
        }

        [Fact]
        [KernelMethod(nameof(OutputEmitKernel))]
        public void OutputEmit()
        {
            const int Length = 64;
            var expected = Enumerable.Range(0, Length)
                .Select(x =>
                {
                    if (Accelerator.AcceleratorType == AcceleratorType.Cuda)
                    {
                        return x + 42;
                    }
                    else
                    {
                        return x;
                    }
                })
                .ToArray();

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }

        internal static void MultipleEmitKernel(
            Index1D index,
            ArrayView1D<double, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit(
                    "{\n\t" +
                    "   .reg .f64 t1;\n\t" +
                    "   add.f64 t1, %1, %2;\n\t" +
                    "   add.f64 %0, t1, %2;\n\t" +
                    "}",
                    out double result,
                    (double)index.X,
                    42.0);
                buffer[index] = result;
            }
            else
            {
                buffer[index] = index;
            }
        }

        [Fact]
        [KernelMethod(nameof(MultipleEmitKernel))]
        public void MultipleEmit()
        {
            const int Length = 64;
            var expected = Enumerable.Range(0, Length)
                .Select(x => (double)x)
                .Select(x =>
                {
                    if (Accelerator.AcceleratorType == AcceleratorType.Cuda)
                    {
                        return x + 42.0 + 42.0;
                    }
                    else
                    {
                        return x;
                    }
                })
                .ToArray();

            using var buffer = Accelerator.Allocate1D<double>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }

        internal static void EscapedEmitKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("mov.u32 %0, %%laneid;", out int lane);
                buffer[index] = lane;
            }
            else
            {
                buffer[index] = index;
            }
        }

        [Fact]
        [KernelMethod(nameof(EscapedEmitKernel))]
        public void EscapedEmit()
        {
            const int Length = 64;
            var expected = Enumerable.Range(0, Length)
                .Select(x =>
                {
                    if (Accelerator.AcceleratorType == AcceleratorType.Cuda)
                    {
                        return x % Accelerator.WarpSize;
                    }
                    else
                    {
                        return x;
                    }
                })
                .ToArray();

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }

        internal static void PredicateEmitKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                var isEven = index.X % 2 == 0;
                CudaAsm.Emit(
                    "{\n\t" +
                    "   @%1 mov.u32 %0, %%laneid;\n\t" +
                    "   @!%1 mov.u32 %0, 42;\n\t" +
                    "}",
                    out int lane,
                    isEven);
                buffer[index] = lane;
            }
            else
            {
                buffer[index] = index;
            }
        }

        [Fact]
        [KernelMethod(nameof(PredicateEmitKernel))]
        public void PredicateEmit()
        {
            const int Length = 64;
            var expected = Enumerable.Range(0, Length)
                .Select(x =>
                {
                    if (Accelerator.AcceleratorType == AcceleratorType.Cuda)
                    {
                        if (x % 2 == 0)
                        {
                            return x % Accelerator.WarpSize;
                        }
                        else
                        {
                            return 42;
                        }
                    }
                    else
                    {
                        return x;
                    }
                })
                .ToArray();

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }

        internal static void Int8EmitKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> buffer)
        {
            if (CudaAsm.IsSupported)
            {
                sbyte truncated = (sbyte)index.X;
                CudaAsm.Emit(
                    "{\n\t" +
                    "   .reg .b32 t1;\n\t" +
                    "   cvt.s32.s8 t1, %1;\n\t" +
                    "   add.s32 %0, t1, 1;\n\t" +
                    "}",
                    out int result,
                    truncated);
                buffer[index] = result;
            }
            else
            {
                buffer[index] = index;
            }
        }

        [Fact]
        [KernelMethod(nameof(Int8EmitKernel))]
        public void Int8Emit()
        {
            const int Length = 512;
            var expected = Enumerable.Range(0, Length)
                .Select(x =>
                {
                    if (Accelerator.AcceleratorType == AcceleratorType.Cuda)
                    {
                        return 1 + (sbyte)x;
                    }
                    else
                    {
                        return x;
                    }
                })
                .ToArray();

            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(Length, buffer.View);
            Verify(buffer.View, expected);
        }
    }
}
