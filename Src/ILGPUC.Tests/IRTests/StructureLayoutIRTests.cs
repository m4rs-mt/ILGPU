// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureLayoutIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace ILGPUC.Tests.IRTests;

// The fields of these structures are only accessed in kernels or by their
// offsets, so the C# compiler cannot see their assignments.
#pragma warning disable CS0649
[StructLayout(LayoutKind.Sequential, Size = 24)]
struct LayoutSized
{
    public int A;
    public short B;
}

struct LayoutWithSized
{
    public byte Tag;
    public LayoutSized Inner;
    public short S;
}
#pragma warning restore CS0649

/// <summary>
/// Kernels for <see cref="StructureLayoutIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class StructureLayoutKernels
{
    public static void SizedParameterKernel(
        Index1D i,
        ArrayView1D<long, Stride1D.Dense> d,
        LayoutWithSized value) =>
        d[i] = value.Tag + value.Inner.A + value.Inner.B + value.S;
}

/// <summary>
/// Verifies that structure types keep the layout they have on the host.
/// </summary>
public sealed class StructureLayoutIRTests
{
    /// <summary>
    /// A parameter of a structure that contains an explicitly sized structure
    /// keeps the offsets of its fields through optimization: the fields behind
    /// the nested structure stay behind its explicit size.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void SizedNestedStructureParameter_KeepsLayout(OptimizationLevel level)
    {
        var kernel = typeof(StructureLayoutKernels).GetMethod(
            nameof(StructureLayoutKernels.SizedParameterKernel))!;
        var properties = new CompilationProperties { OptimizationLevel = level };
        using var helper = new CompilationHelper();
        var module = level.CreateTransformer().Apply(
            properties,
            helper.TypeManager,
            helper.CompileToModule(kernel, properties));

        Parameter? value = null;
        foreach (var parameter in module.EntryPoint.Parameters)
        {
            if (parameter.Name == "value")
                value = parameter;
        }
        var type = Assert.IsType<StructureType>(value?.Type);
        Assert.Equal(Unsafe.SizeOf<LayoutWithSized>(), type.Size);
        Assert.Equal(
            (int)Marshal.OffsetOf<LayoutWithSized>(nameof(LayoutWithSized.S)),
            type.GetOffset(new(type.NumFields - 1)));
    }
}
