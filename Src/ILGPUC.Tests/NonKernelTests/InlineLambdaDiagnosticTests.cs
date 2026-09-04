// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineLambdaDiagnosticTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that validate diagnostic behavior for edge-case inline lambda
/// patterns: assigned captures, reference type captures, captures of the
/// enclosing instance, generic contexts and capturing local functions.
/// </summary>
public sealed class InlineLambdaDiagnosticTests
{
    /// <summary>
    /// Helper: analyzes a program source and returns the analysis result.
    /// </summary>
    private static AnalysisResult AnalyzeSource(string mainBody) =>
        AnalyzeSource(mainBody, out _);

    /// <summary>
    /// Helper: analyzes a program source and returns the analysis result and
    /// the analyzed compilation.
    /// </summary>
    private static AnalysisResult AnalyzeSource(
        string mainBody,
        out CSharpCompilation compilation) =>
        AnalyzeProgram($$"""
            static class Program
            {
                static void Main()
                {
                    using var context = Context.Create(b => b.Default());
                    using var accelerator = context.GetPreferredDevice(preferCPU: true)
                        .CreateAccelerator(context);
                    var stream = accelerator.DefaultStream;

                    using var buffer = stream.Allocate1D<int>(4);
                    var view = buffer.View;

                    {{mainBody}}

                    stream.Synchronize();
                }
            }
            """,
            out compilation);

    /// <summary>
    /// Helper: analyzes the given type declarations, preceded by the usual
    /// ILGPU using directives.
    /// </summary>
    private static AnalysisResult AnalyzeProgram(string declarations) =>
        AnalyzeProgram(declarations, out _);

    /// <summary>
    /// Helper: analyzes the given type declarations and returns the analysis
    /// result and the analyzed compilation.
    /// </summary>
    private static AnalysisResult AnalyzeProgram(
        string declarations,
        out CSharpCompilation compilation)
    {
        var source = $"""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            {declarations}
            """;

        compilation = RoslynCompiler.CreateCompilation(
            [source], "DiagnosticTest");
        return CompilationRewriter.Analyze(compilation);
    }

    /// <summary>
    /// Asserts that exactly one diagnostic with the given id was reported and
    /// that it points at <paramref name="expectedSource"/>.
    /// </summary>
    private static KernelDiagnostic AssertSingleDiagnostic(
        AnalysisResult analysis,
        string expectedId,
        string expectedSource)
    {
        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);

        var location = diagnostic.Location;
        var text = location.SourceTree!.GetText().ToString(location.SourceSpan);
        Assert.Equal(expectedSource, text);
        return diagnostic;
    }

    /// <summary>
    /// Inline kernels that only read captured values report no diagnostics.
    /// </summary>
    [Fact]
    public void ReadOnlyCaptures_NoDiagnostics()
    {
        var analysis = AnalyzeSource("""
            int offset = 3;
            stream.Launch((Index1D)4, index =>
            {
                int local = index;
                local += offset;
                view[index] = local;
            });
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.NotNull(Assert.Single(analysis.UniqueKernels).LiftedKernel);
    }

    /// <summary>
    /// Assigning a captured scalar would be lost: kernels receive captured
    /// values by copy, so the assignment is rejected.
    /// </summary>
    [Fact]
    public void WriteOnlyCapturedScalar_ReportsAssignment()
    {
        var analysis = AnalyzeSource("""
            int result = 0;
            stream.Launch((Index1D)4, index =>
            {
                result = index;
                view[index] = 42;
            });
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.AssignsCaptureId, "result");
        Assert.Null(analysis.UniqueKernels[0].LiftedKernel);
    }

    /// <summary>
    /// A compound assignment of a captured scalar is rejected as well.
    /// </summary>
    [Fact]
    public void ReadWriteCapturedScalar_ReportsAssignment()
    {
        var analysis = AnalyzeSource("""
            int counter = 0;
            stream.Launch((Index1D)4, index =>
            {
                counter += index;
                view[index] = counter;
            });
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.AssignsCaptureId, "counter");
        Assert.Contains("'counter'", diagnostic.Message);
    }

    /// <summary>
    /// A MemoryBuffer capture is lowered to its View. This test validates
    /// the lowering happens correctly and the original buffer name is
    /// preserved in the parameter.
    /// </summary>
    [Fact]
    public void CapturedBuffer_LoweredToViewType()
    {
        var analysis = AnalyzeSource("""
            stream.Launch((Index1D)4, index =>
            {
                buffer.View[index] = index * 3;
            });
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.Single(analysis.UniqueKernels);
        var kernel = analysis.UniqueKernels[0];

        var bufferParam = kernel.Parameters.FirstOrDefault(p => p.Name == "buffer");
        Assert.NotNull(bufferParam);
        Assert.Equal(ParameterKind.View, bufferParam!.Kind);

        // The Type should be the view type, not the buffer type
        Assert.Contains("ArrayView", bufferParam.Type.Name);

        // The lifted kernel receives the view under the buffer's name
        var lifted = kernel.LiftedKernel!.ToString();
        Assert.Contains("buffer[index]", lifted);
        Assert.DoesNotContain("buffer.View", lifted);
    }

    /// <summary>
    /// Capturing an array (a reference type) is rejected.
    /// </summary>
    [Fact]
    public void CapturedReferenceType_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeSource("""
            var values = new int[] { 1, 2, 3, 4 };
            stream.Launch((Index1D)4, index => view[index] = values[index]);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "values");
        Assert.Contains("reference type", diagnostic.Message);
    }

    /// <summary>
    /// Using an instance field captures <c>this</c>, which is rejected at
    /// the member access.
    /// </summary>
    [Fact]
    public void InstanceFieldUse_ReportsThisCapture()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker
            {
                private int _scale = 2;

                public void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * _scale);
                }
            }
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.CapturesThisId, "_scale");
    }

    /// <summary>
    /// Primary-constructor parameters are stored on the instance, so using
    /// one inside an inline kernel captures <c>this</c>.
    /// </summary>
    [Fact]
    public void PrimaryConstructorParameterUse_ReportsThisCapture()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker(int scale)
            {
                public void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * scale);
                }
            }
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.CapturesThisId, "scale");
    }

    /// <summary>
    /// An inline kernel in an instance method that does not touch the
    /// instance is accepted.
    /// </summary>
    [Fact]
    public void InstanceMethodWithoutInstanceUse_NoDiagnostics()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker(AcceleratorStream stream)
            {
                public void Run(ArrayView<int> view, int scale)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * scale);
                }
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var kernel = Assert.Single(analysis.UniqueKernels);
        Assert.Equal(["view", "scale"], kernel.Parameters.Select(p => p.Name));
    }

    /// <summary>
    /// Inline kernels in generic methods are rejected at the lambda.
    /// </summary>
    [Fact]
    public void InlineKernelInGenericMethod_ReportsGenericContext()
    {
        var analysis = AnalyzeProgram("""
            static class Helpers
            {
                public static void Fill<T>(
                    AcceleratorStream stream, ArrayView<T> view, T value)
                    where T : unmanaged
                {
                    stream.Launch(view.IntExtent, index => view[index] = value);
                }
            }
            """);

        AssertSingleDiagnostic(
            analysis,
            KernelDiagnostic.GenericContextId,
            "index => view[index] = value");
    }

    /// <summary>
    /// A local function used as a kernel must not capture variables of its
    /// enclosing method.
    /// </summary>
    [Fact]
    public void CapturingLocalFunctionKernel_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int offset = 1;
            void LocalKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = index + offset;
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "offset");
        Assert.Contains("'LocalKernel'", diagnostic.Message);
    }

    /// <summary>
    /// A non-capturing local function used as a kernel is accepted.
    /// </summary>
    [Fact]
    public void StaticLocalFunctionKernel_NoDiagnostics()
    {
        var analysis = AnalyzeSource("""
            static void LocalKernel(
                Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = index;
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        Assert.Empty(analysis.Diagnostics);
    }

    /// <summary>
    /// An inline kernel calling a local function that captures a variable of
    /// the enclosing method is rejected at the call.
    /// </summary>
    [Fact]
    public void InlineKernelCallingCapturingLocalFunction_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int factor = 3;
            int Scale(int x) => x * factor;
            int Twice(int x) => Scale(x) * 2;
            stream.Launch((Index1D)4, index => view[index] = Twice(index));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "Twice(index)");
        Assert.Contains("'Twice'", diagnostic.Message);
        Assert.Contains("'factor'", diagnostic.Message);
    }

    /// <summary>
    /// A local function used as a kernel that captures a variable through a
    /// local function it calls is rejected at that call.
    /// </summary>
    [Fact]
    public void LocalFunctionKernelCapturingThroughCall_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int factor = 3;
            int Scale(int x) => x * factor;
            void LocalKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = Scale(index);
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "Scale(index)");
        Assert.Contains("through the local function 'Scale'", diagnostic.Message);
    }

    /// <summary>
    /// A launch inside an expression is not rewritten and is rejected.
    /// </summary>
    [Fact]
    public void ExpressionBodiedLaunch_ReportsLaunchNotStatement()
    {
        var analysis = AnalyzeSource("""
            Action launch = () => stream.Launch((Index1D)4, index => view[index] = 1);
            launch();
            """);

        AssertSingleDiagnostic(
            analysis,
            KernelDiagnostic.LaunchNotStatementId,
            "stream.Launch((Index1D)4, index => view[index] = 1)");
    }

    /// <summary>
    /// Inline kernels calling non-static local functions that capture nothing
    /// and capturing variables named like C# keywords or like the leading
    /// launcher parameters are lifted into resolvable kernels.
    /// </summary>
    [Fact]
    public void LiftedKernels_CompileAndResolve()
    {
        var analysis = AnalyzeSource("""
            int Square(int x) => x * x;
            stream.Launch((Index1D)4, index => view[index] = Square(index));

            int @class = 2;
            int config = 3;
            int userExtent = 4;
            stream.Launch((Index1D)4, @event =>
                view[@event] = @class + config + userExtent);
            """,
            out var compilation);
        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(2, analysis.UniqueKernels.Count);

        var rewritten = new CompilationRewriter().Rewrite(
            compilation, analysis, liftInlineKernels: true);
        using var stream = new MemoryStream();
        var emitResult = rewritten.Compilation.Emit(stream);
        Assert.True(
            emitResult.Success,
            string.Join("\n", emitResult.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());
        foreach (var kernel in analysis.UniqueKernels)
        {
            var method = MainKernelProvider.ResolveKernelMethod(assembly, kernel);
            Assert.NotNull(method);
            Assert.Equal(
                kernel.Parameters.Length + 1,
                method!.GetParameters().Length);
        }
    }

    /// <summary>
    /// Diagnostics are formatted in the canonical MSBuild error format with
    /// 1-based line and column numbers.
    /// </summary>
    [Fact]
    public void Diagnostic_FormatsAsMsBuildError()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { }", path: "/src/Program.cs");
        var diagnostic = new KernelDiagnostic(
            KernelDiagnostic.CapturesThisId,
            "Message.",
            Location.Create(tree, new TextSpan(6, 1)));

        Assert.Equal(
            "/src/Program.cs(1,7): error ILGPU1101: Message.",
            diagnostic.ToString());
    }
}
