// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LaunchAnalysisDiagnosticsTests.cs
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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the diagnostics and inputs of the launch analysis that the build command
/// relies on: source generators, launches in generated code, inaccessible types,
/// open public entry points and the length of generated names.
/// </summary>
public sealed class LaunchAnalysisDiagnosticsTests
{
    /// <summary>
    /// A generic wrapper that launches a kernel, shared by the tests.
    /// </summary>
    private const string Launcher = """
        using ILGPU;
        using ILGPU.Runtime;

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
        }

        public static class Launcher
        {
            public static void Run<T>(AcceleratorStream stream) where T : unmanaged
            {
                using var buffer = stream.Allocate1D<T>(4);
                var view = buffer.View;
                stream.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
            }
        }
        """;

    /// <summary>
    /// Creates a compilation of the launcher plus <paramref name="sources"/>.
    /// </summary>
    private static CSharpCompilation Compile(params string[] sources) =>
        RoslynCompiler.CreateCompilation(
            [Launcher, .. sources],
            "AnalysisDiagnosticsTest",
            OutputKind.DynamicallyLinkedLibrary);

    /// <summary>
    /// Returns the closed type arguments of every instantiation of Kernel2.
    /// </summary>
    private static List<string> GetInstantiations(AnalysisResult analysis) =>
        analysis.DispatchStubs
            .SelectMany(s => s.Specializations)
            .Select(k => GenericContext.GetKey(k.ConcreteTypeArgs!.Value))
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void SourceGenerators_RunWithBuildProperties_AndFailuresAreWarnings()
    {
        var directory = Directory.CreateTempSubdirectory("ilgpuc_generators_");
        try
        {
            // A generator assembly with a working and a throwing generator
            var generatorPath = Path.Combine(directory.FullName, "TestGenerators.dll");
            var generatorCompilation = CSharpCompilation.Create(
                "TestGenerators",
                [CSharpSyntaxTree.ParseText("""
                    using Microsoft.CodeAnalysis;

                    [Generator]
                    public sealed class CallerGenerator : IIncrementalGenerator
                    {
                        public void Initialize(
                            IncrementalGeneratorInitializationContext context)
                        {
                            context.RegisterPostInitializationOutput(c => c.AddSource(
                                "Fixed.g.cs",
                                "public static class FixedCaller { public static void " +
                                "Call(ILGPU.Runtime.AcceleratorStream s) => " +
                                "Launcher.Run<float>(s); }"));
                            context.RegisterSourceOutput(
                                context.AnalyzerConfigOptionsProvider,
                                (c, options) =>
                                {
                                    if (options.GlobalOptions.TryGetValue(
                                        "build_property.ILGPUTestType",
                                        out var type))
                                    {
                                        c.AddSource(
                                            "Property.g.cs",
                                            "public static class PropertyCaller { " +
                                            "public static void Call(" +
                                            "ILGPU.Runtime.AcceleratorStream s) => " +
                                            $"Launcher.Run<{type}>(s); }}");
                                    }
                                });
                        }
                    }

                    [Generator]
                    public sealed class ThrowingGenerator : IIncrementalGenerator
                    {
                        public void Initialize(
                            IncrementalGeneratorInitializationContext context) =>
                            context.RegisterPostInitializationOutput(
                                _ => throw new System.InvalidOperationException("boom"));
                    }
                    """)],
                [
                    .. RoslynCompiler.GetDefaultReferences(),
                    MetadataReference.CreateFromFile(
                        typeof(IIncrementalGenerator).Assembly.Location),
                    MetadataReference.CreateFromFile(
                        typeof(ImmutableArray).Assembly.Location),
                ],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var emitResult = generatorCompilation.Emit(generatorPath);
            Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));

            var configPath = Path.Combine(directory.FullName, "test.globalconfig");
            File.WriteAllText(
                configPath,
                "is_global = true\nbuild_property.ILGPUTestType = double\n");

            var compilation = Compile();
            var result = SourceGeneratorRunner.Run(
                compilation,
                (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
                [generatorPath],
                [],
                [configPath]);

            Assert.Equal(2, result.GeneratedTrees.Count);
            var failure = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCodes.SourceGeneratorFailed, failure.Code);
            Assert.False(failure.IsError);

            var analysis = CompilationRewriter.Analyze(
                result.Compilation,
                new AnalysisOptions(GeneratedTrees: result.GeneratedTrees));
            Assert.Equal(["double", "float"], GetInstantiations(analysis));
        }
        finally
        {
            // The generator assembly stays loaded; deleting it may fail on Windows
            try { directory.Delete(recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void LaunchesInGeneratedCode_AreWarnings()
    {
        var compilation = Compile("""
            public static class Generated
            {
                public static void Call(AcceleratorStream stream) =>
                    Launcher.Run<int>(stream);

                public static void Direct(AcceleratorStream stream)
                {
                    using var buffer = stream.Allocate1D<int>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, i => Kernels.Kernel2(i, view));
                }
            }
            """.Replace(
                "public static class Generated",
                "using ILGPU;\nusing ILGPU.Runtime;\npublic static class Generated",
                StringComparison.Ordinal));
        var generated = new HashSet<SyntaxTree> { compilation.SyntaxTrees[1] };

        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(GeneratedTrees: generated));

        // The call from generated code reaches the wrapper; the launch in it is
        // not rewritten
        Assert.Equal(["int"], GetInstantiations(analysis));
        var warning = Assert.Single(analysis.Diagnostics);
        Assert.Equal(BuildDiagnosticCodes.LaunchInGeneratedCode, warning.Code);
        Assert.False(warning.IsError);
        Assert.Single(analysis.AllKernels);
    }

    [Fact]
    public void DelegatesPassedFromGeneratedCode_AreErrors()
    {
        var compilation = Compile(
            """
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
            """,
            """
            static class Generated
            {
                static void Call(
                    ILGPU.Runtime.AcceleratorStream stream,
                    ILGPU.ArrayView<int> view) =>
                    Host.UnaryOp(stream, view, v => v + 1);
            }
            """);
        var generated = new HashSet<SyntaxTree> { compilation.SyntaxTrees[2] };

        var analysis = CompilationRewriter.Analyze(
            compilation,
            new AnalysisOptions(GeneratedTrees: generated));

        // The delegate passed by generated code could not be registered at
        // runtime, so it is an error instead of a launch that throws
        var error = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.UntraceableDelegateArgumentId, error.Code);
        Assert.True(error.IsError);
        Assert.Contains("source-generated code", error.Message);
        Assert.Same(generated.Single(), error.Location!.SourceTree);
    }

    [Theory]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("private protected")]
    public void InaccessibleTypes_AreErrors(string accessibility)
    {
        var analysis = CompilationRewriter.Analyze(Compile($$"""
            public class Program
            {
                {{accessibility}} struct Secret { public int Value; }

                public static void Generic(ILGPU.Runtime.AcceleratorStream stream) =>
                    Launcher.Run<Secret>(stream);

                public static void Direct(ILGPU.Runtime.AcceleratorStream stream)
                {
                    using var buffer = stream.Allocate1D<Box<Secret>>(4);
                    var view = buffer.View;
                    stream.Launch(
                        (ILGPU.Index1D)4,
                        i => Kernels.Kernel2<Box<Secret>>(i, view));
                }
            }
            """));

        Assert.True(analysis.HasErrors);
        Assert.Equal(2, analysis.Diagnostics.Count);
        Assert.All(analysis.Diagnostics, d =>
        {
            Assert.Equal(BuildDiagnosticCodes.InaccessibleKernelType, d.Code);
            Assert.Contains("Program.Secret", d.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void InternalAndProtectedInternalTypes_AreAccessible()
    {
        var analysis = CompilationRewriter.Analyze(Compile("""
            public class Program
            {
                internal struct Shared { public int Value; }
                protected internal struct Family { public int Value; }

                public static void Run(ILGPU.Runtime.AcceleratorStream stream)
                {
                    Launcher.Run<Shared>(stream);
                    Launcher.Run<Family>(stream);
                }
            }
            """));

        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(["Program.Family", "Program.Shared"], GetInstantiations(analysis));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenPublicEntryPoints_WarnOnlyOnRequest(bool warn)
    {
        var analysis = CompilationRewriter.Analyze(
            Compile("""
                public static class Library
                {
                    public static void Process<T>(ILGPU.Runtime.AcceleratorStream s)
                        where T : unmanaged => Helper<T>(s);

                    internal static void Internal<T>(ILGPU.Runtime.AcceleratorStream s)
                        where T : unmanaged => Helper<T>(s);

                    static void Helper<T>(ILGPU.Runtime.AcceleratorStream s)
                        where T : unmanaged => Launcher.Run<T>(s);
                }

                public static class App
                {
                    public static void Main(ILGPU.Runtime.AcceleratorStream s)
                    {
                        Library.Process<int>(s);
                        Library.Internal<long>(s);
                    }
                }
                """),
            new AnalysisOptions(WarnOnOpenPublicEntryPoints: warn));

        Assert.Equal(["int", "long"], GetInstantiations(analysis));
        var reported = analysis.Diagnostics
            .Where(d => d.Code == BuildDiagnosticCodes.OpenPublicEntryPoint)
            .Select(d => d.Message)
            .ToList();
        Assert.Equal(analysis.Diagnostics.Count, reported.Count);
        if (!warn)
        {
            Assert.Empty(reported);
            return;
        }

        // Launcher.Run and Library.Process are public; Helper and Internal are not
        Assert.Equal(2, reported.Count);
        Assert.Contains(
            reported,
            m => m.StartsWith("'Launcher.Run<T>", StringComparison.Ordinal));
        Assert.Contains(
            reported,
            m => m.StartsWith("'Library.Process<T>", StringComparison.Ordinal));
    }

    [Fact]
    public void LongKernelNames_AreShortened()
    {
        var analysis = CompilationRewriter.Analyze(Compile("""
            public struct AStructureWithAVeryLongNameForTesting<T> where T : unmanaged
            {
                public T Value;
            }

            public static class Program
            {
                public static void Run(ILGPU.Runtime.AcceleratorStream stream)
                {
                    Launcher.Run<AStructureWithAVeryLongNameForTesting<
                        AStructureWithAVeryLongNameForTesting<
                            AStructureWithAVeryLongNameForTesting<long>>>>(stream);
                    Launcher.Run<AStructureWithAVeryLongNameForTesting<
                        AStructureWithAVeryLongNameForTesting<
                            AStructureWithAVeryLongNameForTesting<ulong>>>>(stream);
                }
            }
            """));

        var names = analysis.UniqueKernels.Select(k => k.KernelName).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n =>
        {
            Assert.InRange(n.Length, 1, KernelNameAllocator.MaxNameLength);
            Assert.StartsWith(
                "Kernel2_AStructureWithAVeryLongNameForTesting_",
                n,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CompilationErrors_AreReportedWithLocationsAndAHint()
    {
        var compilation = Compile("""
            public static partial class Broken
            {
                public static partial int Missing();
                public static int A() => Undefined1;
                public static int B() => Undefined2;
                public static int C() => Undefined3;
                public static int D() => Undefined4;
                public static int E() => Undefined5;
            }
            """);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Equal(6, errors.Count);

        var diagnostics = new StubCompilationException(errors)
            .ToBuildDiagnostics()
            .ToList();

        // Five errors with their locations, then the summary
        Assert.Equal(6, diagnostics.Count);
        Assert.All(diagnostics, d =>
        {
            Assert.True(d.IsError);
            Assert.Equal(BuildDiagnosticCodes.CompilationErrors, d.Code);
        });
        Assert.All(diagnostics.Take(5), d =>
            Assert.Matches(
                @"^source1\.cs\(\d+,\d+\): error ILGPU1007: CS\d+: ",
                d.ToString()));
        Assert.StartsWith(
            "ILGPUC: error ILGPU1007: ",
            diagnostics[5].ToString(),
            StringComparison.Ordinal);
        Assert.Contains("(6 error(s))", diagnostics[5].Message, StringComparison.Ordinal);
    }
}
