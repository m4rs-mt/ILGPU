// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
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
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that validate diagnostic behavior for edge-case inline lambda
/// patterns: write-only captures, reference type captures, etc.
/// </summary>
public sealed class InlineLambdaDiagnosticTests
{
    /// <summary>
    /// Helper: analyzes a program source and returns the analysis result.
    /// </summary>
    private static AnalysisResult AnalyzeSource(string mainBody)
    {
        var source = $$"""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

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
            """;

        var compilation = RoslynCompiler.CreateCompilation(
            [source], "DiagnosticTest");
        return CompilationRewriter.Analyze(compilation);
    }

    /// <summary>
    /// A write-only captured scalar (result = index) is NOT detected by
    /// DataFlowsIn because it only flows OUT, not IN. This means the
    /// variable is silently dropped — the write has no effect.
    /// This test documents this behavior.
    /// </summary>
    [Fact]
    public void WriteOnlyCapturedScalar_NotInParameters()
    {
        var analysis = AnalyzeSource("""
            int result = 0;
            stream.Launch((Index1D)4, index =>
            {
                result = index;
                view[index] = 42;
            });
            """);

        Assert.Single(analysis.UniqueKernels);
        var kernel = analysis.UniqueKernels[0];

        // 'result' is only written, never read — DataFlowsIn won't capture it.
        // Only 'view' should be captured.
        Assert.DoesNotContain(kernel.Parameters, p => p.Name == "result");
        Assert.Contains(kernel.Parameters, p => p.Name == "view");
    }

    /// <summary>
    /// A read-then-write captured scalar IS detected by DataFlowsIn
    /// because the initial value flows in. However, the write won't
    /// propagate back to the host since the parameter is pass-by-value.
    /// </summary>
    [Fact]
    public void ReadWriteCapturedScalar_IsInParameters()
    {
        var analysis = AnalyzeSource("""
            int counter = 0;
            stream.Launch((Index1D)4, index =>
            {
                counter += index;
                view[index] = counter;
            });
            """);

        Assert.Single(analysis.UniqueKernels);
        var kernel = analysis.UniqueKernels[0];

        // 'counter' is read (+=) so DataFlowsIn captures it.
        // The write won't propagate to host, but it's captured as a parameter.
        Assert.Contains(kernel.Parameters, p => p.Name == "counter");
        Assert.Equal(ParameterKind.Primitive,
            kernel.Parameters.First(p => p.Name == "counter").Kind);
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

        Assert.Single(analysis.UniqueKernels);
        var kernel = analysis.UniqueKernels[0];

        var bufferParam = kernel.Parameters.FirstOrDefault(p => p.Name == "buffer");
        Assert.NotNull(bufferParam);
        Assert.Equal(ParameterKind.View, bufferParam!.Kind);

        // The Type should be the view type, not the buffer type
        Assert.Contains("ArrayView", bufferParam.Type.Name);
    }
}
