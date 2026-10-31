// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursiveTypeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that types referring to themselves through arrays, fields of class
/// types or ever larger instantiations of their generic type definitions are
/// rejected with an unsupported-construct diagnostic at the offending code
/// instead of overflowing the stack while their layouts are computed, while
/// deeply nested instantiations and structures pointing to themselves remain
/// supported.
/// </summary>
public sealed class RecursiveTypeTests
{
    #region Types

    public struct TreeNode
    {
        public int Value;
        public TreeNode[] Children;
    }

    public unsafe struct PointerNode
    {
        public int Value;
        public PointerNode* Next;
    }

    public sealed class ClassNode
    {
        public int Value;
        public ClassNode? Next;
    }

    public struct GenericNode<T>
    {
        public T Value;
        public GenericNode<GenericNode<T>>[] Children;
    }

    public struct ExpandingNode<T>
    {
        public int Value;
        public ExpandingNode<ExpandingNode<T>>[] Children;
    }

    public struct StructNode
    {
        public int Value;
        public ClassLink Link;
    }

    public sealed class ClassLink
    {
        public StructNode Node;
    }

    public struct Box<T>
    {
        public T Value;
    }

    public struct NestedArgument<T>
    {
        public Box<Box<T>> Value;
    }

    public struct GenericTree<T>
    {
        public T Value;
        public GenericTree<T>[] Children;
    }

    public struct GrowingNode<T>
    {
        public int Value;
        public GrowingNode<Box<T>>[] Children;
    }

    public struct MutualNode<T>
    {
        public int Value;
        public MutualLink<T[]>? Link;
    }

    public sealed class MutualLink<T>
    {
        public MutualNode<T>[]? Nodes;
    }

    public struct Wrapper<T>
    {
        public T Value;
    }

    #endregion

    #region Kernels

    public static class Kernels
    {
        public static readonly TreeNode Tree = new()
        {
            Value = 7,
            Children = [new() { Value = 1, Children = [] }],
        };

        public static void StaticTreeKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Tree.Value; // StaticTree
        }

        public static void PointerViewKernel(
            Index1D index,
            ArrayView1D<PointerNode, Stride1D.Dense> data)
        {
            data[index].Value = index;
        }

        public static void CapturedClassKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // CapturedClass
            ClassNode? node = null;
            Func<int, int> f = x => node is null ? x : node.Value;
            data[index] = f(index);
        }

        public static void LocalGenericKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalGeneric
            var node = new GenericNode<int> { Value = index };
            data[index] = node.Value;
        }

        public static void LocalExpandingKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalExpanding
            var node = new ExpandingNode<int> { Value = index };
            data[index] = node.Value;
        }

        public static void LocalGenericTreeKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalGenericTree
            var node = new GenericTree<int> { Value = index };
            data[index] = node.Value;
        }

        public static void LocalStructClassKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalStructClass
            var node = new StructNode { Value = index };
            data[index] = node.Value;
        }

        public static void LocalGrowingKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalGrowing
            var node = new GrowingNode<int> { Value = index };
            data[index] = node.Value;
        }

        public static void LocalMutualKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // LocalMutual
            var node = new MutualNode<int> { Value = index };
            data[index] = node.Value;
        }

        public static void DeeplyNestedKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<
                Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<Wrapper<
                Wrapper<Wrapper<Wrapper<Wrapper<int>>>>>>>>>>>>>>>>>>>> value =
                default;
            value.Value.Value.Value.Value.Value.Value.Value.Value.Value.Value
                .Value.Value.Value.Value.Value.Value.Value.Value.Value.Value =
                index;
            data[index] = value.Value.Value.Value.Value.Value.Value.Value.Value
                .Value.Value.Value.Value.Value.Value.Value.Value.Value.Value
                .Value.Value;
        }
    }

    #endregion

    #region Type Information

    /// <summary>
    /// Fields of reference types occupy a single field without resolving the
    /// layouts of the types they refer to.
    /// </summary>
    [Theory]
    [InlineData(typeof(TreeNode))]
    [InlineData(typeof(PointerNode))]
    [InlineData(typeof(GenericNode<int>))]
    [InlineData(typeof(ExpandingNode<int>))]
    [InlineData(typeof(StructNode))]
    public void RecursiveStructure_ReferencesOccupySingleFields(Type type)
    {
        var typeInfo = new TypeInformationManager().GetTypeInfo(type);
        Assert.Equal(2, typeInfo.NumFlattenedFields);
        Assert.Equal(Enumerable.Range(0, 2), typeInfo.FieldOffsets);
    }

    #endregion

    #region Nested Instantiations

    /// <summary>
    /// Returns the given generic type definition nested in itself the given
    /// number of times around the given innermost type.
    /// </summary>
    private static Type Nest(Type definition, int depth, Type innermost)
    {
        var type = innermost;
        for (int i = 0; i < depth; ++i)
            type = definition.MakeGenericType(type);
        return type;
    }

    /// <summary>
    /// Returns a value tuple of the given number of nested eight-element tuples,
    /// such as the C# compiler creates for tuples of more than seven elements.
    /// </summary>
    private static Type NestValueTuples(int depth)
    {
        var type = typeof(ValueTuple<int>);
        var arguments = Enumerable.Repeat(typeof(int), 8).ToArray();
        for (int i = 0; i < depth; ++i)
        {
            arguments[7] = type;
            type = typeof(ValueTuple<,,,,,,,>).MakeGenericType(arguments);
        }
        return type;
    }

    /// <summary>
    /// Instantiations nested in their own type arguments are finite, however
    /// deeply they are nested, and must not be mistaken for recursive types.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(40)]
    public void NestedInstantiations_AreCreated(int depth)
    {
        var builder = new ModuleBuilder(
            new CompilationProperties(),
            new Generation(),
            Location.Unknown,
            new TypeInformationManager());
        var wrappers = builder.CreateType(Nest(typeof(Wrapper<>), depth, typeof(int)));
        Assert.Equal(sizeof(int), wrappers.Size);
        Assert.IsType<StructureType>(builder.CreateType(NestValueTuples(depth)));
    }

    /// <summary>
    /// A type passing its type parameter to a field type nested in another
    /// instantiation does not instantiate its own definition and is finite.
    /// </summary>
    [Fact]
    public void NestedTypeArgument_IsCreated()
    {
        var builder = new ModuleBuilder(
            new CompilationProperties(),
            new Generation(),
            Location.Unknown,
            new TypeInformationManager());
        var type = builder.CreateType(typeof(NestedArgument<int>));
        Assert.Equal(sizeof(int), type.Size);
    }

    /// <summary>
    /// A kernel taking a view of a structure that points to itself, whose
    /// pointer field is an opaque pointer in its structure type.
    /// </summary>
    [Fact]
    public void PointerView_Compiles()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(nameof(Kernels.PointerViewKernel))
            ?? throw new InvalidOperationException("Kernel method not found.");
        Assert.NotEmpty(
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
    }

    /// <summary>
    /// A kernel using a local of an instantiation nested twenty levels deep.
    /// </summary>
    [Fact]
    public void DeeplyNestedLocal_Compiles()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(nameof(Kernels.DeeplyNestedKernel))
            ?? throw new InvalidOperationException("Kernel method not found.");
        Assert.NotEmpty(
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
    }

    #endregion

    #region Diagnostics

    /// <summary>
    /// The location of the launch site errors without a source location are
    /// reported at.
    /// </summary>
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    /// <summary>
    /// Compiles the given kernel, which must be rejected as an unsupported
    /// construct with the given message at the line of this file marked with the
    /// given comment.
    /// </summary>
    private static void AssertRecursiveType(
        string kernelName,
        string message,
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Kernel method '{kernelName}' not found.");
        var exception = Assert.Throws<InternalCompilerException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));

        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);
        Assert.Equal(
            "Kernel code uses an unsupported construct: " + message,
            diagnostic.Message);

        var location = Assert.IsAssignableFrom<FileLocation>(exception.SourceLocation);
        Assert.Equal(
            Path.GetFileName(sourcePath),
            Path.GetFileName(location.FileName));
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        Assert.Equal(line, location.StartLine);
    }

    [Fact]
    public void StaticTree_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.StaticTreeKernel),
            "Recursive type 'TreeNode' is not supported in GPU kernels: " +
            "it refers to itself through 'TreeNode.Children'.",
            "StaticTree");

    [Fact]
    public void CapturedClass_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.CapturedClassKernel),
            "Recursive type 'ClassNode' is not supported in GPU kernels: " +
            "it refers to itself through 'ClassNode.Next'.",
            "CapturedClass");

    [Fact]
    public void LocalGeneric_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalGenericKernel),
            "Recursive type 'GenericNode<int>' is not supported in GPU kernels: " +
            "it contains ever larger instantiations of 'GenericNode<T>'.",
            "LocalGeneric");

    [Fact]
    public void LocalExpanding_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalExpandingKernel),
            "Recursive type 'ExpandingNode<int>' is not supported in GPU kernels: " +
            "it contains ever larger instantiations of 'ExpandingNode<T>'.",
            "LocalExpanding");

    [Fact]
    public void LocalGenericTree_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalGenericTreeKernel),
            "Recursive type 'GenericTree<int>' is not supported in GPU kernels: " +
            "it refers to itself through 'GenericTree<int>.Children'.",
            "LocalGenericTree");

    [Fact]
    public void LocalStructClass_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalStructClassKernel),
            "Recursive type 'StructNode' is not supported in GPU kernels: " +
            "it refers to itself through 'StructNode.Link.Node'.",
            "LocalStructClass");

    [Fact]
    public void LocalGrowing_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalGrowingKernel),
            "Recursive type 'GrowingNode<int>' is not supported in GPU kernels: " +
            "it contains ever larger instantiations of 'GrowingNode<T>'.",
            "LocalGrowing");

    [Fact]
    public void LocalMutual_ReportsRecursiveType() =>
        AssertRecursiveType(
            nameof(Kernels.LocalMutualKernel),
            "Recursive type 'MutualNode<int>' is not supported in GPU kernels: " +
            "it contains ever larger instantiations of 'MutualNode<T>'.",
            "LocalMutual");

    #endregion
}
