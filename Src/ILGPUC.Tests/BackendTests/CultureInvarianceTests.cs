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
/// ocloc, clang) then rejected with a syntax error. Negative integers must
/// likewise keep the ASCII minus sign in cultures whose negative sign is the
/// Unicode minus sign U+2212 (sv-SE).
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

            // FloatArrayKernel writes 1.5f, 2.5f, 3.5f, 4.5f. Round-trip
            // ("R") emission produces the shortest exact form: 1.5, 2.5, etc.
            Assert.Contains("1.5", result.SourceCode);

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

    public static TheoryData<BackendType, string> NegativeIntegerCases
    {
        get
        {
            var data = new TheoryData<BackendType, string>();
            foreach (var backend in new[]
            {
                BackendType.CPU,
                BackendType.Metal,
                BackendType.Cuda,
                BackendType.OpenCL,
                BackendType.ROCm,
            })
            {
                data.Add(backend, "de-DE");
                data.Add(backend, "sv-SE");
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(NegativeIntegerCases))]
    public void NegativeIntegerLiterals_UseAsciiMinusSign(
        BackendType backend,
        string culture)
    {
        var source = GenerateInCulture(
            nameof(LiteralKernels.NegativeIntegerConstantsKernel),
            backend,
            culture);

        // The CPU backend stores Int8 values as byte
        AssertContainsLiteral(source, backend == BackendType.CPU ? "253" : "-3");
        AssertContainsLiteral(source, "-7");
        AssertContainsLiteral(source, "-5");
        AssertContainsLiteral(source, "-123");
        AssertContainsLiteral(source, "-9876543210L");
        AssertNoLocaleFormatting(source);
    }

    /// <summary>
    /// C# reads an integral literal without a fraction as an integer, which
    /// turns negative zero into zero, so CPU double literals keep a fraction.
    /// </summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    public void CpuIntegralDoubleLiterals_KeepTheirFraction(string culture)
    {
        var source = GenerateInCulture(
            nameof(LiteralKernels.IntegralDoubleConstantsKernel),
            BackendType.CPU,
            culture);

        AssertContainsLiteral(source, "-0.0");
        AssertContainsLiteral(source, "9007199254740992.0");
        AssertContainsLiteral(source, "3.0");
        AssertNoLocaleFormatting(source);
    }

    /// <summary>
    /// Generates the source of a <see cref="LiteralKernels"/> kernel while the
    /// current culture is <paramref name="culture"/>.
    /// </summary>
    private static string GenerateInCulture(
        string kernelName,
        BackendType backend,
        string culture)
    {
        var kernel = typeof(LiteralKernels).GetMethod(kernelName)!;
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUICulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            var cultureInfo = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;

            using var helper = new CompilationHelper(backend);
            var source = helper.GenerateBackendCode(kernel, backend).SourceCode;
            Assert.False(string.IsNullOrWhiteSpace(source));
            return source;
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }

    /// <summary>
    /// Asserts that the source contains the literal as a whole token, so that
    /// e.g. <c>-5</c> does not match inside <c>-5.0f</c> or <c>-55</c>.
    /// </summary>
    private static void AssertContainsLiteral(string source, string literal) =>
        Assert.Matches(
            new Regex(@"(?<![\w.])" + Regex.Escape(literal) + @"(?![\w.])"),
            source);

    private static void AssertNoLocaleFormatting(string source)
    {
        var comma = CommaBetweenDigits.Match(source);
        Assert.False(
            comma.Success,
            $"Locale-formatted number: '{comma.Value}' " +
            $"near: ...{Snippet(source, comma.Index)}...");
        Assert.DoesNotContain('\u2212', source);
    }

    private static string Snippet(string source, int index)
    {
        var start = System.Math.Max(0, index - 20);
        var len = System.Math.Min(60, source.Length - start);
        return source.Substring(start, len).Replace('\n', ' ');
    }
}
