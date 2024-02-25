using System.IO;
using System.Threading.Tasks;
using ILGPU.CodeGeneration;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyTests;
using VerifyXunit;

namespace ILGPU.Analyzers.Tests.Generic;

public static class DiagnosticAnalyzerVerifier<TDiagnosticAnalyzer>
    where TDiagnosticAnalyzer : DiagnosticAnalyzer, new()
{
    public static async Task Verify(string source)
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

        var analyzer = new TDiagnosticAnalyzer();
        var options = new AnalyzerOptions([]);
        var analyzerCompilation =
            new CompilationWithAnalyzers(compilation, [analyzer], options);

        var diagnostics = await analyzerCompilation.GetAnalyzerDiagnosticsAsync();

        var settings = new VerifySettings();
        settings.UseDirectory(Path.Combine("..", "Snapshots"));
        await Verifier.Verify(diagnostics, settings);
    }
}