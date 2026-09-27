// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FieldLoadIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Tests.Framework;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="FieldLoadIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class FieldLoadKernels
{
    unsafe struct PointerHolder
    {
        public int* Pointer;
    }

    ref struct RefHolder
    {
        public ref int Reference;

        public RefHolder(ref int reference)
        {
            Reference = ref reference;
        }
    }

    unsafe struct PaddedHolder
    {
        public int* Pointer;
        public int Pad;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int Read(PointerHolder holder) => *holder.Pointer;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Read(RefHolder holder) => holder.Reference;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int Read(PaddedHolder holder) => *holder.Pointer;

    public static unsafe void PointerHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int local = index;
        data[index] = Read(new PointerHolder { Pointer = &local });
    }

    public static void RefHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int local = index;
        data[index] = Read(new RefHolder(ref local));
    }

    public static unsafe void PaddedHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int local = index;
        data[index] = Read(new PaddedHolder { Pointer = &local, Pad = 1 });
    }
}

/// <summary>
/// Verifies that a field load from a struct value reads the field of the value, also
/// when the struct's only field is a pointer or reference and the struct therefore
/// has the IR type of that pointer: the load must not treat the value as the address
/// of the struct and dereference it.
/// </summary>
public sealed class FieldLoadIRTests
{
    [Theory]
    [InlineData(nameof(FieldLoadKernels.PointerHolderKernel))]
    [InlineData(nameof(FieldLoadKernels.RefHolderKernel))]
    [InlineData(nameof(FieldLoadKernels.PaddedHolderKernel))]
    public void FieldOfStructValue_IsReadFromValue(string kernelName)
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(FieldLoadKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        var module = helper.CompileToModule(kernel);

        int methods = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation || method.Source?.Name != "Read")
                continue;
            ++methods;

            // The pointer field is read from the value, and only the integer it
            // points to is loaded from memory
            var loadedTypes = new List<TypeValue>();
            method.Blocks.ForEachValue<Load>(load => loadedTypes.Add(load.Type));
            var loadedType = Assert.Single(loadedTypes);
            Assert.IsType<PrimitiveType>(loadedType);
        }
        Assert.Equal(1, methods);
    }
}
