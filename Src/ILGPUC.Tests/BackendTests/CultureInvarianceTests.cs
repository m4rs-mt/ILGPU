// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CultureInvarianceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Regression: backend source generation must emit float literals with a `.`
/// decimal separator regardless of the host process culture. Pre-fix, the
/// emitter called `value.ToString("F6")` without an `IFormatProvider`, so
/// running tests on a German-locale machine produced `1,500000f` in the
/// emitted source, which the native compilers (xcrun metal, nvcc, hipcc,
/// ocloc, clang) then rejected with a syntax error.
/// </summary>
public sealed class CultureInvarianceTests
{
    private static readonly Regex CommaBetweenDigits =
        new(@"\d,\d", RegexOptions.Compiled);

    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.OpenCL)]
    [InlineData(BackendType.ROCm)]
    public void FloatLiterals_UseInvariantDecimalSeparator(BackendType backend)
    {
        var kernel = typeof(LocalArrayKernels)
            .GetMethod(nameof(LocalArrayKernels.FloatArrayKernel))!;

        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUICulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            var deDE = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentCulture = deDE;
            Thread.CurrentThread.CurrentUICulture = deDE;

            using var helper = new CompilationHelper(backend);
            var result = helper.GenerateBackendCode(kernel, backend);

            Assert.False(string.IsNullOrWhiteSpace(result.SourceCode));

            // FloatArrayKernel writes 1.5f, 2.5f, 3.5f, 4.5f. After locale-correct
            // emission these must round-trip as 1.500000, 2.500000, etc.
            Assert.Contains("1.500000", result.SourceCode);

            // Conversely, no float literal in the emitted source may contain
            // a comma between digits — that would mean the German decimal
            // separator leaked through.
            var match = CommaBetweenDigits.Match(result.SourceCode);
            Assert.False(
                match.Success,
                $"Emitted source contains locale-formatted float ('{match.Value}') " +
                $"near: ...{Snippet(result.SourceCode, match.Index)}...");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }

    private static string Snippet(string source, int index)
    {
        var start = System.Math.Max(0, index - 20);
        var len = System.Math.Min(60, source.Length - start);
        return source.Substring(start, len).Replace('\n', ' ');
    }
}
