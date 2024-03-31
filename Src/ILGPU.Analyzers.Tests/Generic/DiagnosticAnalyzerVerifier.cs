// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: DiagnosticAnalyzerVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using ILGPU.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyTests;
using VerifyXunit;

namespace ILGPU.Analyzers.Tests.Generic;

public static class DiagnosticAnalyzerVerifier<TDiagnosticAnalyzer>
    where TDiagnosticAnalyzer : DiagnosticAnalyzer, new()
{
    public static async Task Verify(string source,
        Action<VerifySettings> configure = null)
    {
        var ilgpuAssemblies =
            new[]
            {
                typeof(InterleaveFieldsAttribute).Assembly,
                typeof(TDiagnosticAnalyzer).Assembly
            };

        var compilation =
            SourceCompiler.CreateCompilationWithAssemblies("Tests", source,
                ilgpuAssemblies);

        var array = ImmutableArray.Create<DiagnosticAnalyzer>(new TDiagnosticAnalyzer());
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        var analyzerCompilation =
            new CompilationWithAnalyzers(compilation, array, options);

        var diagnostics = await analyzerCompilation.GetAnalyzerDiagnosticsAsync();

        var settings = new VerifySettings();
        settings.UseDirectory(Path.Combine("..", "Snapshots"));

        if (configure is not null)
            configure(settings);

        await Verifier.Verify(diagnostics, settings);
    }
}