// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LaunchRewriteLineTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the <c>#line</c> directives of rewritten launch sites written by the
/// build command: the rewritten sources must compile, and code after a launch
/// must map back to its original line.
/// </summary>
public sealed class LaunchRewriteLineTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(),
        $"ilgpu_line_tests_{Guid.NewGuid():N}");

    /// <summary>
    /// Removes the output directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    /// <summary>
    /// Rewrites the given program to disk and compiles the rewritten source
    /// together with the generated sources, asserting that it has no errors.
    /// </summary>
    /// <returns>The rewritten syntax tree.</returns>
    private SyntaxTree RewriteAndCompile(string program)
    {
        var compilation = RoslynCompiler.CreateCompilation([program], "LineTest");
        int count = new CompilationRewriter().RewriteToDisk(
            compilation,
            CompilationRewriter.Analyze(compilation),
            _outputDir);
        Assert.Equal(1, count);

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.CSharp13);
        var rewritten = CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(_outputDir, "rewritten", "source0.cs")),
            parseOptions,
            path: Path.Combine(_outputDir, "rewritten", "source0.cs"));
        var generated = Directory
            .GetFiles(Path.Combine(_outputDir, "generated"), "*.cs")
            .Select(file => CSharpSyntaxTree.ParseText(
                File.ReadAllText(file),
                parseOptions,
                path: file));

        var result = compilation
            .RemoveAllSyntaxTrees()
            .AddSyntaxTrees([rewritten, .. generated]);
        var errors = result.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return rewritten;
    }

    /// <summary>
    /// Returns the original 1-based line that the given identifier in the
    /// rewritten tree maps to.
    /// </summary>
    private static int GetMappedLine(SyntaxTree tree, string identifier)
    {
        var node = tree.GetRoot().DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Single(d => d.Identifier.ValueText == identifier);
        return node.GetLocation().GetMappedLineSpan().StartLinePosition.Line + 1;
    }

    /// <summary>
    /// Launches that share their line with other code, before or after them,
    /// are rewritten into compilable code, and code after multi-line launches
    /// maps back to its original lines.
    /// </summary>
    [Fact]
    public void LaunchSharingLine_CompilesAndMapsLines()
    {
        // Line numbers below refer to this source
        var tree = RewriteAndCompile("""
            using ILGPU;
            using ILGPU.Runtime;

            static class Program
            {
                static void Run(AcceleratorStream stream, ArrayView<int> view)
                { stream.Launch(view.IntExtent, i => view[i] = 1);
                    int k = 5; stream.Launch(view.IntExtent, i =>
                    {
                        view[i] = k;
                    });
                    int afterFirst = 0;
                    stream.Launch(view.IntExtent, i => view[i] = 2); int sameLine = 1;
                    int afterSecond = afterFirst + sameLine;
                }

                static void Main() { }
            }
            """);

        Assert.Equal(12, GetMappedLine(tree, "afterFirst"));
        Assert.Equal(13, GetMappedLine(tree, "sameLine"));
        Assert.Equal(14, GetMappedLine(tree, "afterSecond"));
    }
}
