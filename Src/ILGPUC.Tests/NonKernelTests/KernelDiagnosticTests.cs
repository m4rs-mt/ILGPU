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

using ILGPUC.IR;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using Microsoft.CodeAnalysis.Text;
using System;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that errors in kernel code are reported as diagnostics (ILGPU12xx) with
/// the identifier, message and source location of the offending code.
/// </summary>
public sealed class KernelDiagnosticTests
{
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
