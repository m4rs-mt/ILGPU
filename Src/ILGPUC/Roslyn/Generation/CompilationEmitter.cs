// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// Emits a final CSharpCompilation (with injected CompiledKernel classes
/// and rewritten call sites) to an in-memory assembly.
/// </summary>
static class CompilationEmitter
{
    /// <summary>
    /// Emits the compilation to in-memory PE and PDB streams.
    /// </summary>
    /// <param name="compilation">The compilation to emit.</param>
    /// <param name="assemblyBytes">
    /// The emitted PE bytes on success; <see langword="null"/> on failure.
    /// </param>
    /// <param name="pdbBytes">
    /// The emitted PDB bytes on success; <see langword="null"/> on failure.
    /// </param>
    /// <returns>The <see cref="EmitResult"/> describing success or diagnostics.</returns>
    public static EmitResult EmitToMemory(
        CSharpCompilation compilation,
        out byte[]? assemblyBytes,
        out byte[]? pdbBytes)
    {
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var result = compilation.Emit(peStream, pdbStream);

        assemblyBytes = result.Success ? peStream.ToArray() : null;
        pdbBytes = result.Success ? pdbStream.ToArray() : null;

        return result;
    }

    /// <summary>
    /// Emits and loads the assembly into the current process for verification.
    /// </summary>
    public static Assembly? EmitAndLoad(
        CSharpCompilation compilation,
        out EmitResult emitResult)
    {
        var result = EmitToMemory(compilation, out var assemblyBytes, out _);
        emitResult = result;

        if (!result.Success || assemblyBytes == null)
            return null;

        return Assembly.Load(assemblyBytes);
    }

    /// <summary>
    /// Emits to a file on disk.
    /// </summary>
    public static EmitResult EmitToFile(
        CSharpCompilation compilation,
        string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var peStream = File.Create(outputPath);
        var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
        using var pdbStream = File.Create(pdbPath);

        return compilation.Emit(peStream, pdbStream);
    }

    /// <summary>
    /// Writes a human-readable summary of errors and warnings from an
    /// <see cref="EmitResult"/> to the given <paramref name="writer"/>.
    /// </summary>
    /// <param name="result">The emit result to summarize.</param>
    /// <param name="writer">The text writer to write diagnostics to.</param>
    /// <param name="maxErrors">Maximum number of error diagnostics to print.</param>
    public static void PrintDiagnostics(
        EmitResult result,
        TextWriter writer,
        int maxErrors = 30)
    {
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        var warnings = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (errors.Count > 0)
        {
            writer.WriteLine($"Emit FAILED — {errors.Count} error(s):");
            foreach (var diag in errors.Take(maxErrors))
                writer.WriteLine($"  {diag}");
            if (errors.Count > maxErrors)
                writer.WriteLine($"  ... and {errors.Count - maxErrors} more");
        }

        if (warnings.Count > 0)
        {
            writer.WriteLine($"{warnings.Count} warning(s):");
            foreach (var diag in warnings.Take(10))
                writer.WriteLine($"  {diag}");
        }

        if (result.Success)
            writer.WriteLine("Emit succeeded.");
    }
}
