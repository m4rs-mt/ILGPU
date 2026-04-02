// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineLambdaAnalysisTests.cs
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
/// Tests that <see cref="KernelExtractor.CreateFromInlineBody"/> correctly discovers
/// and classifies captured variables from inline lambda bodies at launch sites.
/// </summary>
public sealed class InlineLambdaAnalysisTests
{
    /// <summary>
    /// Helper: creates a test program source with the given inline lambda body
    /// and analyzes it. Returns the single unique kernel descriptor.
    /// </summary>
    private static KernelDescriptor AnalyzeInlineLambda(string launchLine)
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
                    using var inputBuffer = stream.Allocate1D<int>(4);
                    using var bufferA = stream.Allocate1D<int>(4);
                    using var bufferB = stream.Allocate1D<int>(4);
                    var view = buffer.View;
                    var inputView = inputBuffer.View;
                    var viewA = bufferA.View;
                    var viewB = bufferB.View;
                    int scalar = 10;
                    float scale = 2.5f;
                    int a = 3;
                    int b = 7;

                    {{launchLine}}

                    stream.Synchronize();
                }
            }
            """;

        var compilation = RoslynCompiler.CreateCompilation(
            [source], "InlineLambdaTest");
        var analysis = CompilationRewriter.Analyze(compilation);

        Assert.Single(analysis.UniqueKernels);
        return analysis.UniqueKernels[0];
    }

    [Fact]
    public void CapturedView_DetectedAsViewParameter()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { view[index] = 42; });");

        Assert.Null(kernel.KernelMethod);
        Assert.NotNull(kernel.KernelBody);
        Assert.Single(kernel.Parameters);

        var param = kernel.Parameters[0];
        Assert.Equal("view", param.Name);
        Assert.Equal(ParameterKind.View, param.Kind);
    }

    [Fact]
    public void CapturedViewAndScalar_BothDetected()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { view[index] = scalar; });");

        Assert.Null(kernel.KernelMethod);
        Assert.Equal(2, kernel.Parameters.Length);

        var viewParam = kernel.Parameters.FirstOrDefault(p => p.Name == "view");
        var scalarParam = kernel.Parameters.FirstOrDefault(p => p.Name == "scalar");

        Assert.NotNull(viewParam);
        Assert.NotNull(scalarParam);
        Assert.Equal(ParameterKind.View, viewParam!.Kind);
        Assert.Equal(ParameterKind.Primitive, scalarParam!.Kind);
    }

    [Fact]
    public void CapturedMultipleViews_AllDetected()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => " +
            "{ view[index] = viewA[index] + viewB[index]; });");

        Assert.Null(kernel.KernelMethod);
        Assert.Equal(3, kernel.Parameters.Length);

        Assert.All(kernel.Parameters, p =>
            Assert.Equal(ParameterKind.View, p.Kind));
    }

    [Fact]
    public void CapturedFloat_DetectedAsPrimitive()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => " +
            "{ view[index] = (int)(index * scale); });");

        Assert.Equal(2, kernel.Parameters.Length);

        var scaleParam = kernel.Parameters.FirstOrDefault(p => p.Name == "scale");
        Assert.NotNull(scaleParam);
        Assert.Equal(ParameterKind.Primitive, scaleParam!.Kind);
    }

    [Fact]
    public void IndexParam_NotCaptured()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { view[index] = 42; });");

        // The lambda parameter 'index' must not appear in captures
        Assert.DoesNotContain(kernel.Parameters, p => p.Name == "index");
    }

    [Fact]
    public void InlineLambda_KernelNameIncludesLine()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { view[index] = 42; });");

        Assert.StartsWith("InlineKernel_L", kernel.KernelName);
        Assert.Null(kernel.KernelMethod);
        Assert.NotNull(kernel.KernelBody);
    }

    [Fact]
    public void InlineLambda_VariantIsAuto1D()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { view[index] = 42; });");

        Assert.Equal(LaunchVariant.Auto1D, kernel.Variant);
    }

    [Fact]
    public void CapturedMultipleScalars_AllDetected()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => " +
            "{ view[index] = index * a + b; });");

        Assert.Equal(3, kernel.Parameters.Length);

        var aParam = kernel.Parameters.FirstOrDefault(p => p.Name == "a");
        var bParam = kernel.Parameters.FirstOrDefault(p => p.Name == "b");
        Assert.NotNull(aParam);
        Assert.NotNull(bParam);
        Assert.Equal(ParameterKind.Primitive, aParam!.Kind);
        Assert.Equal(ParameterKind.Primitive, bParam!.Kind);
    }

    [Fact]
    public void ExpressionBodyLambda_CapturingViewAndScalar()
    {
        // Expression body (no braces) — tests the non-block DataFlowAnalysis path
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => view[index] = scalar);");

        Assert.Null(kernel.KernelMethod);
        Assert.NotNull(kernel.KernelBody);
        Assert.Equal(2, kernel.Parameters.Length);

        var viewParam = kernel.Parameters.FirstOrDefault(p => p.Name == "view");
        var scalarParam = kernel.Parameters.FirstOrDefault(p => p.Name == "scalar");
        Assert.NotNull(viewParam);
        Assert.NotNull(scalarParam);
    }

    [Fact]
    public void CapturedBuffer_LoweredToView()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { buffer.View[index] = 42; });");

        // buffer (MemoryBuffer1D) should be lowered to its View (ArrayView1D)
        Assert.Single(kernel.Parameters);

        var param = kernel.Parameters[0];
        Assert.Equal("buffer", param.Name);
        Assert.Equal(ParameterKind.View, param.Kind);

        // The source expression should be "buffer.View", not just "buffer"
        Assert.Equal("buffer.View", param.SourceExpression.ToString());
    }

    [Fact]
    public void CapturedBufferAndScalar_BothDetected()
    {
        var kernel = AnalyzeInlineLambda(
            "stream.Launch((Index1D)4, index => { buffer.View[index] = scalar; });");

        Assert.Equal(2, kernel.Parameters.Length);

        var bufferParam = kernel.Parameters.FirstOrDefault(p => p.Name == "buffer");
        var scalarParam = kernel.Parameters.FirstOrDefault(p => p.Name == "scalar");

        Assert.NotNull(bufferParam);
        Assert.NotNull(scalarParam);
        Assert.Equal(ParameterKind.View, bufferParam!.Kind);
        Assert.Equal("buffer.View", bufferParam.SourceExpression.ToString());
        Assert.Equal(ParameterKind.Primitive, scalarParam!.Kind);
    }

    [Fact]
    public void MultiStatementLambda_LocalVarsNotCaptured()
    {
        var kernel = AnalyzeInlineLambda(
            """
            stream.Launch((Index1D)4, index =>
            {
                int temp = inputView[index] * 2;
                view[index] = temp + scalar;
            });
            """);

        // 'temp' is a local, not a capture — should not appear in Parameters
        Assert.DoesNotContain(kernel.Parameters, p => p.Name == "temp");

        // The real captures: view, inputView, scalar
        Assert.Equal(3, kernel.Parameters.Length);
        Assert.Contains(kernel.Parameters, p => p.Name == "view");
        Assert.Contains(kernel.Parameters, p => p.Name == "inputView");
        Assert.Contains(kernel.Parameters, p => p.Name == "scalar");
    }
}
