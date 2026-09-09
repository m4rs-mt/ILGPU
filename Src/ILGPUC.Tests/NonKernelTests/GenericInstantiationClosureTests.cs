// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericInstantiationClosureTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that kernels launched with open type arguments from generic code are
/// instantiated with exactly the closed type arguments that reach their launch
/// sites, through any depth of generic wrappers, and that the rewritten
/// compilation compiles.
/// </summary>
public sealed class GenericInstantiationClosureTests
{
    /// <summary>
    /// Kernels and helper types shared by all test sources.
    /// </summary>
    private const string Prelude = """
        using System;
        using ILGPU;
        using ILGPU.Runtime;

        public struct Pair<T> where T : unmanaged { public T A; public T B; }
        public struct Box<T> where T : unmanaged { public T Value; }

        public static class Kernels
        {
            public static void Kernel2<T>(
                Index1D index,
                ArrayView1D<T, Stride1D.Dense> data)
                where T : unmanaged
            {
                data[index] = default;
            }

            public static void Scaled<T>(
                Index1D index,
                ArrayView1D<T, Stride1D.Dense> data,
                int scale = 3)
                where T : unmanaged
            {
                data[index] = default;
            }

            public static void Kernel3<T, U>(
                Index1D index,
                ArrayView1D<T, Stride1D.Dense> a,
                ArrayView1D<U, Stride1D.Dense> b)
                where T : unmanaged
                where U : unmanaged
            {
                a[index] = default;
                b[index] = default;
            }
        }

        """;

    /// <summary>
    /// Creates a compilation of the prelude plus <paramref name="source"/> and
    /// asserts that it compiles.
    /// </summary>
    private static CSharpCompilation Compile(string source)
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [Prelude + source],
            "ClosureTest",
            OutputKind.DynamicallyLinkedLibrary);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return compilation;
    }

    /// <summary>
    /// Analyzes <paramref name="source"/> and asserts that the rewritten
    /// compilation, with stub kernels and dispatch stubs, compiles.
    /// </summary>
    private static AnalysisResult Analyze(
        string source,
        GenericInstantiationLimits? limits = null,
        bool expectErrors = false)
    {
        var compilation = Compile(source);
        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(limits));
        if (expectErrors)
            return analysis;

        Assert.False(
            analysis.HasErrors,
            string.Join("\n", analysis.Diagnostics));
        var rewritten = new CompilationRewriter().Rewrite(compilation, analysis);
        var errors = rewritten.Compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(
            errors.Count == 0,
            string.Join("\n", errors) + "\n\n" +
            string.Join("\n\n", rewritten.GeneratedSources));
        return analysis;
    }

    /// <summary>
    /// Returns the closed type arguments of every specialization of the kernel
    /// method <paramref name="kernelName"/>, sorted.
    /// </summary>
    private static List<string> GetInstantiations(
        AnalysisResult analysis,
        string kernelName) =>
        analysis.DispatchStubs
            .Where(s => s.KernelDefinition.Name == kernelName)
            .SelectMany(s => s.Specializations)
            .Select(k => GenericContext.GetKey(k.ConcreteTypeArgs!.Value))
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void WrapperChain_DepthTwo_FindsOuterInstantiation()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Outer<double>(stream);

                static void Outer<T>(AcceleratorStream stream) where T : unmanaged
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    Run<T>(stream, buffer.View);
                }

                static void Run<T>(
                    AcceleratorStream stream,
                    ArrayView1D<T, Stride1D.Dense> data)
                    where T : unmanaged
                {
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, data));
                }
            }
            """);

        Assert.Equal(["double"], GetInstantiations(analysis, "Kernel2"));
        Assert.Single(analysis.DispatchStubs);
        Assert.Empty(analysis.Diagnostics);
    }

    [Fact]
    public void WrapperChain_DepthFive_FindsAllInstantiations()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    L1<float>(stream);
                    L1<int>(stream);
                }

                static void L1<T>(AcceleratorStream s) where T : unmanaged => L2<T>(s);
                static void L2<T>(AcceleratorStream s) where T : unmanaged => L3<T>(s);
                static void L3<T>(AcceleratorStream s) where T : unmanaged => L4<T>(s);
                static void L4<T>(AcceleratorStream s) where T : unmanaged => L5<T>(s);

                static void L5<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Equal(["float", "int"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void GenericContainingType_NonGenericLauncher_IsResolved()
    {
        var analysis = Analyze("""
            public sealed class Runner<T> where T : unmanaged
            {
                public void Run(AcceleratorStream stream)
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    new Runner<long>().Run(stream);
                    var runner = new Runner<short>();
                    runner.Run(stream);
                }
            }
            """);

        Assert.Equal(["long", "short"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void KernelInGenericType_IsDispatchedOverContainingTypeArgument()
    {
        var analysis = Analyze("""
            public static class Ops<T> where T : unmanaged
            {
                public static void Fill(
                    Index1D index,
                    ArrayView1D<T, Stride1D.Dense> data)
                {
                    data[index] = default;
                }

                public static void Run(AcceleratorStream stream)
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Fill(i, view));
                }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Ops<float>.Run(stream);
                    Ops<byte>.Run(stream);
                }
            }
            """);

        Assert.Equal(["byte", "float"], GetInstantiations(analysis, "Fill"));
        var specialization = analysis.UniqueKernels.First(
            k => k.KernelMethod!.Name == "Fill");
        Assert.False(GenericContext.IsOpen(specialization.KernelMethod!));
    }

    [Fact]
    public void PartialApplicationAndPermutation_MapArgumentsCorrectly()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Partial<float>(stream);
                    Permute<byte, ushort>(stream);
                }

                static void Partial<T>(AcceleratorStream s) where T : unmanaged =>
                    Run<int, T>(s);

                static void Permute<A, B>(AcceleratorStream s)
                    where A : unmanaged
                    where B : unmanaged =>
                    Run<B, A>(s);

                static void Run<U, V>(AcceleratorStream s)
                    where U : unmanaged
                    where V : unmanaged
                {
                    using var a = s.Allocate1D<U>(4);
                    using var b = s.Allocate1D<V>(4);
                    var va = a.View;
                    var vb = b.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel3(i, va, vb));
                }
            }
            """);

        Assert.Equal(
            ["int, float", "ushort, byte"],
            GetInstantiations(analysis, "Kernel3"));
    }

    [Fact]
    public void ConstructedTypeArgument_IsSubstitutedDeeply()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Wrap<float>(stream);

                static void Wrap<T>(AcceleratorStream s) where T : unmanaged =>
                    Run<Pair<T>>(s);

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Equal(["Pair<float>"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void ConstructedKernelArgument_AtLaunchSite_IsResolved()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<double>(stream);

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<Box<T>>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Equal(["Box<double>"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void DirectAndMutualRecursion_Terminate()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Ping<int>(stream, 3);

                static void Ping<T>(AcceleratorStream s, int n) where T : unmanaged
                {
                    if (n <= 0)
                    {
                        using var buffer = s.Allocate1D<T>(4);
                        var view = buffer.View;
                        s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                        return;
                    }
                    Ping<T>(s, n - 1);
                    Pong<T>(s, n - 1);
                }

                static void Pong<T>(AcceleratorStream s, int n) where T : unmanaged =>
                    Ping<T>(s, n);
            }
            """);

        Assert.Equal(["int"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void PolymorphicRecursion_HitsTypeDepthLimit()
    {
        var analysis = Analyze(
            """
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Grow<int>(stream, 3);

                static void Grow<T>(AcceleratorStream s, int n) where T : unmanaged
                {
                    if (n > 0)
                        Grow<Box<T>>(s, n - 1);
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """,
            new GenericInstantiationLimits(MaxTypeDepth: 4));

        // The limit truncates with a warning; shallower instantiations compile
        var warning = Assert.Single(analysis.Diagnostics);
        Assert.False(warning.IsError);
        Assert.Equal(BuildDiagnosticCodes.TypeDepthExceeded, warning.Code);
        Assert.Contains("Box<Box<Box<Box<int>>>>", warning.Message);
        Assert.StartsWith("source0.cs(", warning.ToString());
        Assert.Contains("Box<Box<Box<int>>>", GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void InstantiationsPerDefinition_LimitIsReported()
    {
        var analysis = Analyze(
            """
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Run<sbyte>(stream);
                    Run<byte>(stream);
                    Run<short>(stream);
                }

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """,
            new GenericInstantiationLimits(MaxInstantiationsPerDefinition: 2));

        var warning = Assert.Single(analysis.Diagnostics);
        Assert.False(warning.IsError);
        Assert.Equal(BuildDiagnosticCodes.DefinitionInstantiationsExceeded, warning.Code);
        Assert.Equal(2, GetInstantiations(analysis, "Kernel2").Count);
    }

    [Fact]
    public void MethodGroupConversion_IsAnEdge()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Action<AcceleratorStream> run = Run<uint>;
                    run(stream);
                }

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Equal(["uint"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void InterfaceDispatch_ThroughObjectCreation_IsFound()
    {
        var analysis = Analyze("""
            public interface IRunner
            {
                void Run(AcceleratorStream stream);
            }

            public sealed class Runner<T> : IRunner where T : unmanaged
            {
                public void Run(AcceleratorStream stream)
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    IRunner runner = new Runner<ulong>();
                    runner.Run(stream);
                }
            }
            """);

        Assert.Equal(["ulong"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void LocalFunctionAndLambda_InheritTypeParameters()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<double>(stream);

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    void Launch<U>() where U : unmanaged
                    {
                        using var a = s.Allocate1D<T>(4);
                        using var b = s.Allocate1D<U>(4);
                        var va = a.View;
                        var vb = b.View;
                        s.Launch((Index1D)4, i => Kernels.Kernel3(i, va, vb));
                    }

                    Action action = () => Launch<int>();
                    action();
                }
            }
            """);

        Assert.Equal(["double, int"], GetInstantiations(analysis, "Kernel3"));
    }

    [Fact]
    public void OpenPublicEntryPoint_ReportsWarning()
    {
        var analysis = Analyze("""
            public static class Library
            {
                public static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        var stub = Assert.Single(analysis.DispatchStubs);
        Assert.Empty(stub.Specializations);
        var warning = Assert.Single(analysis.Diagnostics);
        Assert.False(warning.IsError);
        Assert.Equal(BuildDiagnosticCodes.OpenLaunchSite, warning.Code);
        Assert.Contains("Library.Run<T>", warning.Message);
        Assert.Contains("source0.cs(", warning.ToString());
        Assert.Contains(": warning ILGPU1001: ", warning.ToString());
    }

    [Fact]
    public void TwoLaunchSites_ShareOneDispatchStub()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    A<float>(stream);
                    B<int>(stream);
                    B<float>(stream);
                }

                static void A<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }

                static void B<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        var stub = Assert.Single(analysis.DispatchStubs);
        Assert.Equal(2, stub.LaunchSites.Length);
        Assert.Equal(["float", "int"], GetInstantiations(analysis, "Kernel2"));
        Assert.All(
            analysis.AllKernels,
            k => Assert.Equal(stub.Name, k.KernelName));
    }

    [Fact]
    public void ConstructedArguments_DoNotCollideInNames()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Run<Pair<int>>(stream);
                    Run<Pair<float>>(stream);
                }

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Equal(
            ["Pair<float>", "Pair<int>"],
            GetInstantiations(analysis, "Kernel2"));
        var names = analysis.UniqueKernels.Select(k => k.KernelName).ToList();
        Assert.Equal(
            names.Count,
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DirectClosedLaunches_OfOneGenericKernel_GetDistinctNames()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    using var a = stream.Allocate1D<int>(4);
                    using var b = stream.Allocate1D<float>(4);
                    var va = a.View;
                    var vb = b.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, va));
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, vb));
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, va));
                }
            }
            """);

        Assert.Empty(analysis.DispatchStubs);
        Assert.Equal(2, analysis.UniqueKernels.Count);
        Assert.NotEqual(
            analysis.UniqueKernels[0].KernelName,
            analysis.UniqueKernels[1].KernelName);
        Assert.Equal(
            analysis.AllKernels[0].KernelName,
            analysis.AllKernels[2].KernelName);
    }

    [Fact]
    public void DirectAndWrappedLaunches_CoexistWithoutClashes()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    using var a = stream.Allocate1D<int>(4);
                    var va = a.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, va));
                    Run<int>(stream);
                }

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        Assert.Single(analysis.DispatchStubs);
        var names = analysis.UniqueKernels.Select(k => k.KernelName)
            .Append(analysis.DispatchStubs[0].Name)
            .ToList();
        Assert.Equal(
            names.Count,
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void UnrelatedGenericCode_IsNotInstantiated()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Unrelated<int>();
                    Unrelated<float>();
                    Run<double>(stream);
                }

                static int Unrelated<T>() => Unrelated2<T, T>();
                static int Unrelated2<T, U>() => 0;

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """);

        // Entry and Run<double>; Unrelated<T> cannot reach a kernel
        Assert.Equal(["double"], GetInstantiations(analysis, "Kernel2"));
        Assert.Equal(2, analysis.InstantiationCount);
    }

    [Fact]
    public void NamedArguments_BindByParameter()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<int, float>(stream);

                static void Run<T, U>(AcceleratorStream s)
                    where T : unmanaged
                    where U : unmanaged
                {
                    using var a = s.Allocate1D<T>(4);
                    using var b = s.Allocate1D<U>(4);
                    var va = a.View;
                    var vb = b.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel3(i, b: vb, a: va));
                }

                static void Direct(AcceleratorStream s)
                {
                    using var a = s.Allocate1D<int>(4);
                    using var b = s.Allocate1D<float>(4);
                    var va = a.View;
                    var vb = b.View;
                    s.Launch((Index1D)4, i => Kernels.Kernel3(i, b: vb, a: va));
                }
            }
            """);

        Assert.All(analysis.AllKernels, kernel =>
        {
            Assert.Equal(["a", "b"], kernel.Parameters.Select(p => p.Name));
            Assert.Equal(
                ["va", "vb"],
                kernel.Parameters.Select(p => p.SourceExpression.ToString()));
        });
    }

    [Fact]
    public void OmittedOptionalArguments_UseTheDefault()
    {
        var analysis = Analyze("""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Run<int>(stream);
                    using var buffer = stream.Allocate1D<float>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Kernels.Scaled(i, view));
                }

                static void Run<T>(AcceleratorStream s) where T : unmanaged
                {
                    using var buffer = s.Allocate1D<T>(4);
                    var view = buffer.View;
                    s.Launch((Index1D)4, i => Kernels.Scaled(i, view));
                }
            }
            """);

        Assert.All(analysis.AllKernels, kernel =>
            Assert.Equal(["data", "scale"], kernel.Parameters.Select(p => p.Name)));
    }

    /// <summary>
    /// The body of a typical generic launch, shared by the dispatch tests.
    /// </summary>
    private const string LaunchBody = """
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
        """;

    [Fact]
    public void GenericInterfaceMethod_IsResolvedThroughTheImplementation()
    {
        var analysis = Analyze($$"""
            public interface IRunner
            {
                void Run<T>(AcceleratorStream stream) where T : unmanaged;
            }

            public sealed class Runner : IRunner
            {
                public void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
            {{LaunchBody}}
                }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    IRunner runner = new Runner();
                    runner.Run<float>(stream);
                }
            }
            """);

        Assert.Equal(["float"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void VirtualOverride_AndInheritedBaseMethod_AreResolved()
    {
        var analysis = Analyze($$"""
            public abstract class Base<T> where T : unmanaged
            {
                public abstract void Run(AcceleratorStream stream);

                public void Inherited(AcceleratorStream stream)
                {
            {{LaunchBody}}
                }
            }

            public sealed class Derived<T> : Base<T> where T : unmanaged
            {
                public override void Run(AcceleratorStream stream)
                {
            {{LaunchBody}}
                }
            }

            public sealed class Fixed : Base<long>
            {
                public override void Run(AcceleratorStream stream) { }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Base<int> a = new Derived<int>();
                    a.Run(stream);
                    Base<long> b = new Fixed();
                    b.Inherited(stream);
                }
            }
            """);

        Assert.Equal(["int", "long"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void ConstrainedAndStaticAbstractCalls_AreResolvedOnTheTypeArgument()
    {
        var analysis = Analyze($$"""
            public interface IRun
            {
                static abstract void RunStatic(AcceleratorStream stream);
                void RunInstance(AcceleratorStream stream);
            }

            public struct Op<T> : IRun where T : unmanaged
            {
                public static void RunStatic(AcceleratorStream stream)
                {
            {{LaunchBody}}
                }

                public void RunInstance(AcceleratorStream stream) => RunStatic(stream);
            }

            public sealed class Factory<T> : IRun where T : unmanaged
            {
                public static void RunStatic(AcceleratorStream stream) { }

                public void RunInstance(AcceleratorStream stream) =>
                    Op<T>.RunStatic(stream);
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Static<Op<byte>>(stream);
                    Instance<Op<short>>(stream);
                    Create<Factory<uint>>(stream);
                    IRun boxed = default(Op<ulong>);
                    boxed.RunInstance(stream);
                }

                static void Static<TOp>(AcceleratorStream s) where TOp : IRun =>
                    TOp.RunStatic(s);

                static void Instance<TOp>(AcceleratorStream s) where TOp : IRun =>
                    default(TOp)!.RunInstance(s);

                static void Create<TOp>(AcceleratorStream s) where TOp : IRun, new() =>
                    new TOp().RunInstance(s);
            }
            """);

        Assert.Equal(
            ["byte", "short", "uint", "ulong"],
            GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void PartialMethods_AreFollowed()
    {
        var analysis = Analyze($$"""
            public static partial class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<sbyte>(stream);

                static partial void Run<T>(AcceleratorStream stream) where T : unmanaged;
            }

            public static partial class Program
            {
                static partial void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
            {{LaunchBody}}
                }
            }
            """);

        Assert.Equal(["sbyte"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void UncalledMethods_DoNotCreateInstantiations()
    {
        var analysis = Analyze($$"""
            public sealed class Runner<T> where T : unmanaged
            {
                public void Run(AcceleratorStream stream)
                {
            {{LaunchBody}}
                }

                public Runner<Box<T>> Lift() => new Runner<Box<T>>();
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    new Runner<int>().Run(stream);
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(["int"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void UntrackedTypeParameters_DoNotHitLimits()
    {
        var analysis = Analyze($$"""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<int, int>(stream, 3);

                static void Run<T, U>(AcceleratorStream stream, int depth)
                    where T : unmanaged
                    where U : unmanaged
                {
                    if (depth > 0)
                        Run<T, Box<U>>(stream, depth - 1);
            {{LaunchBody}}
                }
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(["int"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void NestedTypesInGenericTypes_AreNamedCorrectly()
    {
        var analysis = Analyze($$"""
            public struct Outer<T> where T : unmanaged
            {
                public struct Cell { public T Value; }
            }

            public static class Program
            {
                public static void Entry(AcceleratorStream stream) =>
                    Run<Outer<int>.Cell>(stream);

                static void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
            {{LaunchBody}}
                }
            }
            """);

        Assert.Equal(["Outer<int>.Cell"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void LaunchSiteNamespaces_DoNotShadowTypeArguments()
    {
        var analysis = Analyze($$"""
            namespace Lib
            {
                public struct Item { public int Value; }
            }

            namespace App.Lib
            {
                public struct Item { public long Value; }
            }

            namespace App
            {
                public static class Program
                {
                    public static void Entry(AcceleratorStream stream) =>
                        Run<global::Lib.Item>(stream);

                    static void Run<T>(AcceleratorStream stream) where T : unmanaged
                    {
            {{LaunchBody}}
                    }
                }
            }
            """);

        Assert.Equal(["Lib.Item"], GetInstantiations(analysis, "Kernel2"));
    }

    [Fact]
    public void TupleElementNames_DoNotDuplicateInstantiations()
    {
        var analysis = Analyze($$"""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Run<(int a, int b)>(stream);
                    Run<(int x, int y)>(stream);
                    Run<(int, int)>(stream);
                }

                static void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
            {{LaunchBody}}
                }
            }
            """);

        Assert.Single(GetInstantiations(analysis, "Kernel2"));
    }

    [Theory]
    [InlineData("""
        static void Run<T>(AcceleratorStream stream) where T : unmanaged
        {
            using var buffer = stream.Allocate1D<T>(4);
            var view = buffer.View;
            static void Local(Index1D i, ArrayView1D<T, Stride1D.Dense> v) =>
                v[i] = default;
            stream.Launch((Index1D)4, i => Local(i, view));
        }
        """)]
    [InlineData("""
        static void Run<T>(AcceleratorStream stream) where T : unmanaged
        {
            using var buffer = stream.Allocate1D<T>(4);
            var view = buffer.View;
            stream.Launch((Index1D)4, i => { view[i] = default; });
        }
        """)]
    public void LocalKernels_InGenericCode_AreCompiledPerInstantiation(string run)
    {
        var analysis = Analyze(
            $$"""
            public static class Program
            {
                public static void Entry(AcceleratorStream stream)
                {
                    Run<int>(stream);
                    Run<float>(stream);
                }

                {{run}}
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var stub = Assert.Single(analysis.DispatchStubs);
        Assert.Equal(
            ["float", "int"],
            stub.Specializations
                .Select(k => GenericContext.GetKey(k.ConcreteTypeArgs!.Value))
                .Order(StringComparer.Ordinal));
    }
    /// <summary>
    /// Sources in which C# runs a generic wrapper without an invocation in the
    /// operation tree, each paired with the type argument that reaches the launch.
    /// The wrapper is always <c>Launcher.Run&lt;T&gt;</c>.
    /// </summary>
    public static TheoryData<string, string> HiddenCalls => new()
    {
        {
            """
            public struct Batches<T> where T : unmanaged
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public int Current => 0;
                    public bool MoveNext()
                    {
                        Launcher.Run<T>(Program.Stream);
                        return false;
                    }
                }
            }
            public static partial class Program
            {
                public static void Entry() { foreach (var _ in new Batches<float>()) { } }
            }
            """,
            "float"
        },
        {
            """
            public sealed class Sequence<T> : System.Collections.Generic.IEnumerable<int>
                where T : unmanaged
            {
                public System.Collections.Generic.IEnumerator<int> GetEnumerator()
                {
                    Launcher.Run<T>(Program.Stream);
                    yield break;
                }
                System.Collections.IEnumerator System.Collections.IEnumerable
                    .GetEnumerator() => GetEnumerator();
            }
            public static partial class Program
            {
                public static void Entry()
                {
                    System.Collections.Generic.IEnumerable<int> items =
                        new Sequence<double>();
                    foreach (var _ in items) { }
                }
            }
            """,
            "double"
        },
        {
            """
            public struct Resource<T> : IDisposable where T : unmanaged
            {
                public void Dispose() => Launcher.Run<T>(Program.Stream);
            }
            public static partial class Program
            {
                public static void Entry() { using var resource = new Resource<long>(); }
            }
            """,
            "long"
        },
        {
            """
            public sealed class Resource<T> : IDisposable where T : unmanaged
            {
                public void Dispose() => Launcher.Run<T>(Program.Stream);
            }
            public static partial class Program
            {
                public static void Entry() => Use(new Resource<short>());
                static void Use<R>(R resource) where R : IDisposable
                {
                    using (resource) { }
                }
            }
            """,
            "short"
        },
        {
            """
            public struct Splitter<T> where T : unmanaged
            {
                public void Deconstruct(out int a, out int b)
                {
                    Launcher.Run<T>(Program.Stream);
                    a = b = 0;
                }
            }
            public static partial class Program
            {
                public static void Entry() { var (a, b) = new Splitter<int>(); }
            }
            """,
            "int"
        },
        {
            """
            public struct Awaitable<T> where T : unmanaged
            {
                public Awaiter GetAwaiter() => default;
                public struct Awaiter : System.Runtime.CompilerServices.INotifyCompletion
                {
                    public bool IsCompleted => true;
                    public void OnCompleted(Action continuation) { }
                    public void GetResult() => Launcher.Run<T>(Program.Stream);
                }
            }
            public static partial class Program
            {
                public static async System.Threading.Tasks.Task Entry() =>
                    await new Awaitable<byte>();
            }
            """,
            "byte"
        },
        {
            """
            public sealed class Window<T> where T : unmanaged
            {
                public int Length => 1;
                public int this[int index]
                {
                    get { Launcher.Run<T>(Program.Stream); return 0; }
                }
            }
            public static partial class Program
            {
                public static int Entry() => new Window<uint>()[^1];
            }
            """,
            "uint"
        },
        {
            """
            public static class Cache<T> where T : unmanaged
            {
                public static readonly int Ready = Prepare();
                static int Prepare() { Launcher.Run<T>(Program.Stream); return 1; }
            }
            public static partial class Program
            {
                public static int Entry() => Cache<float>.Ready;
            }
            """,
            "float"
        },
        {
            """
            public static class Holder<T> where T : unmanaged
            {
                static Holder() => Launcher.Run<T>(Program.Stream);
                public static void Touch() { }
            }
            public static partial class Program
            {
                public static void Entry() => Holder<double>.Touch();
            }
            """,
            "double"
        },
        {
            """
            public sealed class Worker<T> where T : unmanaged
            {
                private readonly int prepared = Prepare();
                public Worker(int unused) { }
                public Worker() : this(0) { }
                static int Prepare() { Launcher.Run<T>(Program.Stream); return 1; }
            }
            public static partial class Program
            {
                public static void Entry() => new Worker<ushort>();
            }
            """,
            "ushort"
        },
    };

    [Theory]
    [MemberData(nameof(HiddenCalls))]
    public void HiddenCalls_AreFollowed(string source, string expected)
    {
        var analysis = Analyze($$"""
            public static class Launcher
            {
                public static void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
            {{LaunchBody}}
                }
            }

            public static partial class Program
            {
                public static AcceleratorStream Stream { get; set; } = null!;
            }

            {{source}}
            """);

        Assert.Equal([expected], GetInstantiations(analysis, "Kernel2"));
    }
}
