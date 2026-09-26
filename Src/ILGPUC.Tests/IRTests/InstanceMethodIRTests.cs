// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InstanceMethodIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Tests.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="InstanceMethodIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class InstanceMethodKernels
{
    unsafe struct PointerHolder
    {
        public int* Pointer;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public readonly int Read() => *Pointer;
    }

    struct ValueHolder
    {
        public int Value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public readonly int Read() => Value;
    }

    public static unsafe void PointerHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int local = index;
        var holder = new PointerHolder { Pointer = &local };
        data[index] = holder.Read();
    }

    public static void ValueHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var holder = new ValueHolder { Value = index };
        data[index] = holder.Read();
    }
}

/// <summary>
/// Verifies that the <c>this</c> parameter of a struct method is a pointer to the
/// struct, which is what its callers pass, also when the struct's only field is a
/// pointer and the struct therefore has the IR type of that pointer.
/// </summary>
public sealed class InstanceMethodIRTests
{
    [Theory]
    [InlineData(nameof(InstanceMethodKernels.PointerHolderKernel), true)]
    [InlineData(nameof(InstanceMethodKernels.ValueHolderKernel), false)]
    public void StructThis_IsPointerToStruct(string kernelName, bool holdsPointer)
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(InstanceMethodKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        var module = helper.CompileToModule(kernel);

        int calls = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            method.ForEachValue<MethodCall>(call =>
            {
                if (call.Target.Source?.Name != "Read")
                    return;
                ++calls;

                // The struct is a pointer to an integer or an integer, and this
                // points to it
                var self = call.Target.Parameters[0];
                var pointer = Assert.IsType<PointerType>(self.Type);
                if (holdsPointer)
                    Assert.IsType<PointerType>(pointer.ElementType);
                else
                    Assert.IsType<PrimitiveType>(pointer.ElementType);
                Assert.Equal(self.Type, call.Arguments[0].Type);
            });
        }
        Assert.Equal(1, calls);
    }
}
