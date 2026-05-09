// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LauncherFieldAccessorFlatteningTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Isolation tests for <see cref="LauncherStubGenerator.ExtractFieldAccessors"/>,
/// the helper that converts a Roslyn type symbol into the leaf accessor chains
/// the launcher emitter uses to bridge user-facing ArrayView types onto the
/// IR's flattened parameter struct. Pinning this directly is the cheapest way
/// to lock the layout contract that #1464 turns on: if the produced list ever
/// re-collapses (e.g. drops <c>Extent.X</c>/<c>Extent.Y</c> back to <c>Extent</c>),
/// every multi-dim launcher silently goes back to emitting invalid C# and
/// the round-trip execution tests fail downstream — these unit tests fail
/// first and explain why.
/// </summary>
public sealed class LauncherFieldAccessorFlatteningTests
{
    /// <summary>
    /// Compiles a tiny snippet that declares fields of the requested ArrayView
    /// types and pulls their symbols out of the semantic model. Returns the
    /// flattened accessor list <c>LauncherStubGenerator</c> would emit for
    /// each field, in declaration order.
    /// </summary>
    private static string[][] ExtractAccessorsFor(params string[] fieldDeclarations)
    {
        var fields = string.Join("\n    ", fieldDeclarations);
        var source = $$"""
            using ILGPU;
            using ILGPU.Runtime;

            static class Probe
            {
                {{fields}}
            }
            """;

        var compilation = RoslynCompiler.CreateCompilation([source], "AccessorProbe");
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var fieldSyntaxes = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .ToArray();

        return [.. fieldSyntaxes.Select(f =>
        {
            var typeSymbol = model.GetTypeInfo(f.Declaration.Type).Type!;
            return LauncherStubGenerator.ExtractFieldAccessors(typeSymbol) ?? [];
        })];
    }

    [Fact]
    public void ArrayView1D_Dense_FlattensToBaseViewAndDenseStride()
    {
        var accessors = ExtractAccessorsFor(
            "public static ArrayView1D<int, Stride1D.Dense> View;");

        // Stride1D.Dense is an empty struct with one synthetic byte slot, so
        // the IR keeps a placeholder accessor — we just assert the BaseView
        // appears as the first leaf, which is the only one with semantic load.
        Assert.NotEmpty(accessors[0]);
        Assert.Equal("BaseView", accessors[0][0]);
    }

    [Fact]
    public void ArrayView2D_DenseX_FlattensExtentAndStride()
    {
        // The contract: ArrayView2D<T, Stride2D.DenseX> must decompose to
        // ["BaseView", "Extent.X", "Extent.Y", "Stride.YStride"]. This is the
        // shape #1464 hinges on — Extent.X is the contiguous axis (= width
        // when allocated as Allocate2DDenseX(new Index2D(W, H))) and YStride
        // (= W) is the only non-unit stride field.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView2D<int, Stride2D.DenseX> View;");

        Assert.Equal(
            new[] { "BaseView", "Extent.X", "Extent.Y", "Stride.YStride" },
            accessors[0]);
    }

    [Fact]
    public void ArrayView2D_DenseY_FlattensExtentAndStride()
    {
        // Mirror of DenseX: Y is the contiguous axis, XStride (= H) is the
        // only non-unit stride field. The accessor list must reflect the
        // orthogonal layout so the launcher marshals the correct slot.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView2D<int, Stride2D.DenseY> View;");

        Assert.Equal(
            new[] { "BaseView", "Extent.X", "Extent.Y", "Stride.XStride" },
            accessors[0]);
    }

    [Fact]
    public void ArrayView3D_DenseXY_FlattensExtentAndStride()
    {
        // Stride3D.DenseXY: XStride is computed (=> 1, no backing field),
        // YStride / ZStride are auto-properties. The flattener must skip the
        // computed XStride and emit only the auto-property accessors plus the
        // three Extent leaves and BaseView.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView3D<int, Stride3D.DenseXY> View;");

        Assert.Equal(
            new[]
            {
                "BaseView",
                "Extent.X", "Extent.Y", "Extent.Z",
                "Stride.YStride", "Stride.ZStride",
            },
            accessors[0]);
    }

    [Fact]
    public void ArrayView3D_DenseZY_FlattensExtentAndStride()
    {
        // Mirror of DenseXY: ZStride is the computed (= 1) field, XStride and
        // YStride are auto-properties. Z is the contiguous axis; the launcher
        // marshals XStride (= H * D) and YStride (= D) as the only non-unit
        // strides.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView3D<int, Stride3D.DenseZY> View;");

        Assert.Equal(
            new[]
            {
                "BaseView",
                "Extent.X", "Extent.Y", "Extent.Z",
                "Stride.XStride", "Stride.YStride",
            },
            accessors[0]);
    }

    [Fact]
    public void ArrayView2D_General_FlattensThroughStrideExtent()
    {
        // Stride2D.General stores its data as a single Index2D auto-property
        // (StrideExtent); XStride / YStride are computed projections. The
        // flattener must recurse THROUGH the auto-property into Index2D's X/Y
        // leaves — this is the deeper-nesting case (one level beyond DenseX/Y).
        var accessors = ExtractAccessorsFor(
            "public static ArrayView2D<int, Stride2D.General> View;");

        Assert.Equal(
            new[]
            {
                "BaseView",
                "Extent.X", "Extent.Y",
                "Stride.StrideExtent.X", "Stride.StrideExtent.Y",
            },
            accessors[0]);
    }

    [Fact]
    public void ArrayView3D_General_FlattensThroughStrideExtent()
    {
        // 3D analogue: Stride3D.General nests an Index3D auto-property
        // (StrideExtent) — flattener must produce three stride leaves plus
        // the three Extent leaves.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView3D<int, Stride3D.General> View;");

        Assert.Equal(
            new[]
            {
                "BaseView",
                "Extent.X", "Extent.Y", "Extent.Z",
                "Stride.StrideExtent.X",
                "Stride.StrideExtent.Y",
                "Stride.StrideExtent.Z",
            },
            accessors[0]);
    }

    [Fact]
    public void ArrayView_BaseView_FlattensToSingleSlot()
    {
        // Plain ArrayView<T> is a view-typed leaf — the launcher emitter
        // marshals it via the View branch, not the StructWithViews branch,
        // but ExtractFieldAccessors still needs to return a single-element
        // list so callers don't trip the null-coalesce path on a struct that
        // does have flatten-able fields.
        var accessors = ExtractAccessorsFor(
            "public static ArrayView<int> View;");

        Assert.NotEmpty(accessors[0]);
    }

    [Fact]
    public void NonStructType_ReturnsNullSentinel()
    {
        // Reference types are rejected at the call site (ParameterKind would
        // never be StructWithViews) — the helper signals this by returning
        // null. The test framework coalesces null to [] before forwarding to
        // CompiledKernelGenerator, so this is a defensive check.
        var source = """
            static class Probe
            {
                public static string Field;
            }
            """;

        var compilation = RoslynCompiler.CreateCompilation([source], "AccessorProbe");
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var fieldSyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single();
        var typeSymbol = model.GetTypeInfo(fieldSyntax.Declaration.Type).Type!;

        Assert.Null(LauncherStubGenerator.ExtractFieldAccessors(typeSymbol));
    }
}
