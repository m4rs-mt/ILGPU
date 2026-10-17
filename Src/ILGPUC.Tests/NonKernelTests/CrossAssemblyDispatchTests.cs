// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CrossAssemblyDispatchTests.cs
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
using System.IO;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that a library summarizes the kernel launches of its public generic
/// methods, and that an application referencing it instantiates and registers the
/// kernels for the type arguments it uses.
/// </summary>
public sealed class CrossAssemblyDispatchTests
{
    private const string Library = """
        using ILGPU;
        using ILGPU.Runtime;

        namespace TestLib;

        public struct Pair<T> where T : unmanaged { public T A; public T B; }

        public static class Kernels
        {
            public static void Fill<T>(
                Index1D index,
                ArrayView1D<T, Stride1D.Dense> data)
                where T : unmanaged
            {
                data[index] = default;
            }
        }

        public static class Library
        {
            public static void Run<T>(AcceleratorStream stream) where T : unmanaged
            {
                Launch<T>(stream);
                Launch<Pair<T>>(stream);
            }

            public static void RunInt(AcceleratorStream stream) => Run<int>(stream);

            static void Launch<T>(AcceleratorStream stream) where T : unmanaged
            {
                using var buffer = stream.Allocate1D<T>(4);
                var view = buffer.View;
                stream.Launch((Index1D)4, i => Kernels.Fill(i, view));
            }
        }
        """;

    /// <summary>
    /// Analyzes the library, emits it with its generated code and returns the
    /// analysis and a reference to the emitted assembly.
    /// </summary>
    private static (AnalysisResult Analysis, MetadataReference Reference) BuildLibrary()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [Library],
            "TestLib",
            OutputKind.DynamicallyLinkedLibrary);
        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(AssemblyName: "TestLib"));
        Assert.False(analysis.HasErrors, string.Join("\n", analysis.Diagnostics));

        var rewritten = new CompilationRewriter().Rewrite(compilation, analysis);
        using var stream = new MemoryStream();
        var emit = rewritten.Compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return (analysis, MetadataReference.CreateFromImage(stream.ToArray()));
    }

    [Fact]
    public void Library_SummarizesPublicGenericLaunches()
    {
        var (analysis, _) = BuildLibrary();

        // The open launch is covered by the summary, so no ILGPU1001
        Assert.DoesNotContain(
            analysis.Diagnostics,
            d => d.Code == BuildDiagnosticCodes.OpenLaunchSite);

        // Run<T> is public; Launch<T> is not; RunInt has no type parameters
        var entry = Assert.Single(analysis.Summaries!);
        Assert.StartsWith(
            "M:TestLib.Library.Run``1",
            entry.Method,
            StringComparison.Ordinal);
        var stub = Assert.Single(analysis.DispatchStubs);
        Assert.Equal($"TestLib:{stub.Name}", stub.DispatchKey);
        Assert.Equal(
            ["!0", "TestLib|TestLib.Pair`1<!0>"],
            entry.Launches
                .Select(l => l.TypeArguments.Single())
                .Order(StringComparer.Ordinal));
        Assert.All(entry.Launches, l =>
        {
            Assert.Equal(stub.DispatchKey, l.DispatchKey);
            Assert.Equal(1, l.SkippedParameters);
        });

        // RunInt's instantiation is compiled into the library itself
        Assert.Equal(
            ["TestLib.Pair<int>", "int"],
            stub.Specializations
                .Select(s => GenericContext.GetKey(s.ConcreteTypeArgs!.Value))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Application_InstantiatesAndRegistersLibraryKernels()
    {
        var (library, reference) = BuildLibrary();
        var stub = Assert.Single(library.DispatchStubs);

        var compilation = RoslynCompiler.CreateCompilation(
            ["""
            using ILGPU.Runtime;

            public struct Point { public float X; public float Y; }

            public static class App
            {
                public static void Main(AcceleratorStream stream) => Run<Point>(stream);

                static void Run<T>(AcceleratorStream stream) where T : unmanaged
                {
                    TestLib.Library.Run<float>(stream);
                    TestLib.Library.Run<T>(stream);
                }
            }
            """],
            "TestApp",
            OutputKind.DynamicallyLinkedLibrary,
            [reference]);
        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(AssemblyName: "TestApp"));
        Assert.False(analysis.HasErrors, string.Join("\n", analysis.Diagnostics));

        var registrations = analysis.DispatchRegistrations!;
        Assert.All(registrations, r => Assert.Equal(stub.DispatchKey, r.Key));
        Assert.Equal(
            ["Point", "TestLib.Pair<Point>", "TestLib.Pair<float>", "float"],
            registrations
                .Select(r => GenericContext.GetKey(r.Kernel.ConcreteTypeArgs!.Value))
                .Order(StringComparer.Ordinal));
        Assert.All(registrations, r => Assert.Contains(r.Kernel, analysis.UniqueKernels));

        // The registrations compile against the generated kernel classes
        var rewritten = new CompilationRewriter().Rewrite(compilation, analysis);
        var errors = rewritten.Compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    /// <summary>
    /// Analyzes a library consisting of <paramref name="source"/> with summaries
    /// enabled, or an executable if <paramref name="isLibrary"/> is false.
    /// </summary>
    private static AnalysisResult AnalyzeLibrary(string source, bool isLibrary = true)
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [source],
            "TestLib",
            OutputKind.DynamicallyLinkedLibrary);
        return CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(AssemblyName: "TestLib", IsLibrary: isLibrary));
    }

    /// <summary>
    /// An inline lambda kernel in a public generic method has no definition
    /// that the builds of other assemblies can compile for their type arguments,
    /// so it is a build error (ILGPU1012) instead of a launch that throws in the
    /// application, even if the library instantiates it itself. Other
    /// assemblies do not call executables, which compile it for their own type
    /// arguments and summarize no launches.
    /// </summary>
    [Fact]
    public void Library_InlineKernelInPublicGenericMethod_IsError()
    {
        const string Source = """
            using ILGPU;
            using ILGPU.Runtime;

            namespace TestLib;

            public static class Library
            {
                public static void Fill<T>(AcceleratorStream stream, T value)
                    where T : unmanaged
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => view[i] = value);
                }

                public static void FillInt(AcceleratorStream stream) =>
                    Fill(stream, 1);
            }
            """;
        var analysis = AnalyzeLibrary(Source);

        Assert.True(analysis.HasErrors);
        var error = Assert.Single(analysis.Diagnostics);
        Assert.True(error.IsError);
        Assert.Equal(BuildDiagnosticCodes.LocalKernelInPublicEntryPoint, error.Code);
        Assert.Equal(
            "source0.cs(13,35): error ILGPU1012: The kernel launched in " +
            "'TestLib.Library.Fill<T>(ILGPU.Runtime.AcceleratorStream, T)' is a " +
            "lambda expression or local function, which other assemblies cannot " +
            "compile for their own type arguments, but " +
            "'TestLib.Library.Fill<T>(ILGPU.Runtime.AcceleratorStream, T)' is " +
            "visible to them. Make the kernel a method, or make " +
            "'TestLib.Library.Fill<T>(ILGPU.Runtime.AcceleratorStream, T)' " +
            "internal.",
            error.ToString());

        var executable = AnalyzeLibrary(Source, isLibrary: false);
        Assert.Empty(executable.Diagnostics);
        Assert.Empty(executable.Summaries!);
        var stub = Assert.Single(executable.DispatchStubs);
        Assert.Null(stub.DispatchKey);
        var specialization = Assert.Single(stub.Specializations);
        Assert.Equal(
            "int",
            GenericContext.GetKey(specialization.ConcreteTypeArgs!.Value));
    }

    /// <summary>
    /// A local function kernel in a private method that a public generic method
    /// passes its type arguments to is reported at the local function, naming
    /// the public method.
    /// </summary>
    [Fact]
    public void Library_LocalFunctionKernelReachedFromPublicMethod_IsError()
    {
        var analysis = AnalyzeLibrary("""
            using ILGPU;
            using ILGPU.Runtime;

            namespace TestLib;

            public class Library
            {
                protected static void Run<T>(AcceleratorStream stream)
                    where T : unmanaged => Launch<T>(stream);

                static void Launch<T>(AcceleratorStream stream) where T : unmanaged
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    static void Clear(Index1D i, ArrayView1D<T, Stride1D.Dense> v) =>
                        v[i] = default;
                    stream.Launch((Index1D)4, i => Clear(i, view));
                }
            }
            """);

        var error = Assert.Single(analysis.Diagnostics);
        Assert.True(error.IsError);
        Assert.Equal(BuildDiagnosticCodes.LocalKernelInPublicEntryPoint, error.Code);
        Assert.StartsWith("source0.cs(15,21): error ILGPU1012: ", error.ToString());
        Assert.Contains(
            "The kernel launched in 'TestLib.Library.Launch<T>(" +
            "ILGPU.Runtime.AcceleratorStream)', which 'TestLib.Library.Run<T>(" +
            "ILGPU.Runtime.AcceleratorStream)' passes its type arguments to, is a " +
            "lambda expression or local function",
            error.Message);
    }

    /// <summary>
    /// Methods that other assemblies cannot call keep inline lambda kernels:
    /// an internal generic method that nothing calls only produces warning
    /// ILGPU1001, and a private one called with closed type arguments produces
    /// nothing.
    /// </summary>
    [Fact]
    public void Library_InlineKernelInInternalGenericMethod_Builds()
    {
        var analysis = AnalyzeLibrary("""
            using ILGPU;
            using ILGPU.Runtime;

            namespace TestLib;

            public static class Library
            {
                internal static void Fill<T>(AcceleratorStream stream, T value)
                    where T : unmanaged
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => view[i] = value);
                }
            }

            public static class Runner
            {
                public static void Run(AcceleratorStream stream) =>
                    Clear<float>(stream);

                static void Clear<T>(AcceleratorStream stream) where T : unmanaged
                {
                    using var buffer = stream.Allocate1D<T>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => view[i] = default);
                }
            }
            """);

        Assert.False(analysis.HasErrors, string.Join("\n", analysis.Diagnostics));
        Assert.Empty(analysis.Summaries!);
        var warning = Assert.Single(analysis.Diagnostics);
        Assert.Equal(BuildDiagnosticCodes.OpenLaunchSite, warning.Code);
        Assert.Contains("TestLib.Library.Fill<T>", warning.Message);
    }

    /// <summary>
    /// Delegates passed by other assemblies cannot be traced, so summaries
    /// never cover delegate parameters: a public generic method forwarding a
    /// delegate to a launching method is a build error (ILGPU1111) in library
    /// builds as well, instead of a launch that throws in the application.
    /// </summary>
    [Fact]
    public void Library_PublicMethodForwardingDelegate_IsError()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            ["""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            namespace TestLib;

            static class Host
            {
                internal static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, i => view[i] = op(view[i]));
                }
            }

            public static class Library
            {
                public static void Apply<T>(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                    where T : unmanaged =>
                    Host.UnaryOp(stream, view, op);
            }
            """],
            "TestLib",
            OutputKind.DynamicallyLinkedLibrary);
        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(AssemblyName: "TestLib"));

        Assert.True(analysis.HasErrors);
        var error = Assert.Single(
            analysis.Diagnostics,
            d => d.Code == KernelDiagnostic.VisibleLaunchingMethodId);
        Assert.True(error.IsError);
        Assert.Contains("'TestLib.Library.Apply' forwards", error.Message);
        Assert.Null(analysis.Summaries);
    }

    /// <summary>
    /// The keys of delegates passed to launching methods are prefixed with the
    /// name of the assembly being built, like the dispatch keys, so equally
    /// named launching methods of different assemblies cannot share keys.
    /// </summary>
    [Fact]
    public void DelegateKeys_UseAssemblyName()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            ["""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            static class Host
            {
                internal static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, i => view[i] = op(view[i]));
                }

                static void Run(AcceleratorStream stream, ArrayView<int> view) =>
                    UnaryOp(stream, view, v => -v);
            }
            """],
            "ILGPUKernelCompilation",
            OutputKind.DynamicallyLinkedLibrary);
        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(AssemblyName: "MyApp"));

        Assert.False(analysis.HasErrors, string.Join("\n", analysis.Diagnostics));
        var registration = Assert.Single(analysis.DelegateRegistrations).Value;
        Assert.Equal(["MyApp:Host.UnaryOp.op#0"], registration.Keys);
    }
}
