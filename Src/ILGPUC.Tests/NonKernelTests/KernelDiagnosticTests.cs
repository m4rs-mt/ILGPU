// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDiagnosticTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that errors in kernel code are reported as diagnostics (ILGPU12xx) with
/// the identifier, message and source location of the offending code.
/// </summary>
public sealed class KernelDiagnosticTests
{
    #region Kernels

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry. Every rejected line is marked with a comment naming the
    // test, which the test looks up to check the reported location.
    private static class Kernels
    {
        private static int Apply(Func<int, int> f, int x) => f(x);

        private static Func<int, int> Make() => x => x * 2;

        // r1_returned: the delegate returned from Make was bound to the only
        // other lambda of the method, computing 303 304 305 306
        public static void ReturnedDelegateWithOtherLambdaKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int a = Apply(x => x + 3, 0);
            var f = Make();
            data[index] = f(data[index]) + 100 * a; // ReturnedDelegateWithOtherLambda
        }

        private static int ApplyIn(in Func<int, int> f, int x) =>
            f(x); // DelegateFromRefParameter

        public static void DelegateFromRefParameterKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> g = x => x + 1;
            data[index] = ApplyIn(in g, data[index]);
        }
    }

    /// <summary>
    /// Compiles the given kernel, which must be rejected with a diagnostic.
    /// </summary>
    private static KernelCompilationException Compile(string kernelName)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Kernel method '{kernelName}' not found.");
        return Assert.Throws<KernelCompilationException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
    }

    /// <summary>
    /// Asserts that the given diagnostic has the given identifier and message
    /// and points at the line of this file marked with the given comment.
    /// </summary>
    private static void AssertDiagnostic(
        KernelCompilationException exception,
        string id,
        string message,
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        Assert.Equal(id, exception.Id);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);

        var location = Assert.IsAssignableFrom<FileLocation>(exception.Location);
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

    /// <summary>
    /// Builds the given kernel class <c>K</c>, whose method <c>Kern</c> is
    /// launched on four elements, through the analysis and kernel compilation
    /// of ilgpuc builds, and asserts that the build fails with the given error
    /// at the line marked with <c>// error</c>.
    /// </summary>
    private static async Task AssertBuildErrorAsync(
        string kernelClass,
        string id,
        string message)
    {
        var source = $$"""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            {{kernelClass}}

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
                    stream.Launch((Index1D)4, index => K.Kern(index, view));
                    stream.Synchronize();
                }
            }
            """;
        var compilation = RoslynCompiler.CreateCompilation(
            [source],
            "KernelDiagnosticTest");
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);

        var exception = await Assert.ThrowsAsync<KernelDiagnosticException>(() =>
            MainKernelProvider.CreateAsync(
                compilation,
                analysis,
                [CompilationHelper.CreateBackend(BackendType.CPU)],
                new KernelCompiler()));
        int line = Array.FindIndex(
            source.Split('\n'),
            l => l.TrimEnd().EndsWith("// error", StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, "Marker '// error' not found");
        var diagnostic = exception.Diagnostic.ToString();
        Assert.StartsWith($"source0.cs({line},", diagnostic, StringComparison.Ordinal);
        Assert.Contains(
            $"): error {id}: {message}",
            diagnostic,
            StringComparison.Ordinal);
    }

    #endregion

    #region Unresolved Delegates (ILGPU1202)

    // r1_returned: computed 303 304 305 306 instead of 300 302 304 306 on CPU
    // and Metal, as the returned delegate was bound to the other lambda
    [Fact]
    public Task ReturnedDelegateWithOtherLambda_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Apply(Func<int, int> g, int x) => g(x);
                static Func<int, int> Make() => x => x * 2;
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int a = Apply(x => x + 3, 0);
                    var f = Make();
                    data[i] = f(data[i]) + 100 * a; // error
                }
            }
            """,
            "ILGPU1202",
            "Cannot statically resolve the delegate invoked in method 'Kern': " +
            "it is returned from method 'Make'.");

    [Fact]
    public void ReturnedDelegateWithOtherLambda_ReportsUnresolved() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ReturnedDelegateWithOtherLambdaKernel)),
            "ILGPU1202",
            "Cannot statically resolve the delegate invoked in method " +
            "'ReturnedDelegateWithOtherLambdaKernel': it is returned from " +
            "method 'Make'.",
            "ReturnedDelegateWithOtherLambda");

    [Fact]
    public void DelegateFromRefParameter_ReportsUnresolved() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateFromRefParameterKernel)),
            "ILGPU1202",
            "Cannot statically resolve the delegate invoked in method " +
            "'ApplyIn': it is loaded through a reference.",
            "DelegateFromRefParameter");

    #endregion

    #region Build Diagnostics

    /// <summary>
    /// The location of the launch site errors without a source location are
    /// reported at.
    /// </summary>
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    private static string? Report(Exception exception) =>
        KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic)
        ? diagnostic!.ToString()
        : null;

    [Fact]
    public void KernelCompilationException_IsReportedAtItsSourceLocation()
    {
        var exception = new KernelCompilationException(
            "ILGPU1201",
            "Message.",
            new FileLocation("/src/Kernels.cs", 9, 20, 12, 12));

        Assert.Equal(
            "/src/Kernels.cs(12,9): error ILGPU1201: Message.",
            Report(exception));
    }

    [Fact]
    public void KernelCompilationException_WithoutLocation_IsReportedAtLaunchSite()
    {
        var exception = new KernelCompilationException(
            "ILGPU1202",
            "Message.",
            Location.Unknown);

        Assert.Equal(
            "/src/Launch.cs(5,9): error ILGPU1202: Message.",
            Report(exception));
    }

    [Fact]
    public void UnsupportedConstruct_IsReportedAsILGPU1200()
    {
        var location = new FileLocation("/src/Kernels.cs", 5, 9, 3, 3);
        var exception = location.GetNotSupportedException("Not supported {0}", "X");

        Assert.Equal(
            "/src/Kernels.cs(3,5): error ILGPU1200: Kernel code uses an " +
            "unsupported construct: Not supported X",
            Report(exception));
    }

    [Fact]
    public void UnsupportedConstruct_WithoutLocation_IsReportedAtLaunchSite()
    {
        Assert.Equal(
            "/src/Launch.cs(5,9): error ILGPU1200: Kernel code uses an " +
            "unsupported construct: Not supported",
            Report(new NotSupportedException("Not supported")));
    }

    [Fact]
    public void InternalCompilerErrors_AreNotReportedAsDiagnostics()
    {
        var location = new FileLocation("/src/Kernels.cs", 5, 9, 3, 3);
        Assert.Null(Report(location.GetInvalidOperationException()));
        Assert.Null(Report(new InvalidOperationException()));
    }

    #endregion
}
