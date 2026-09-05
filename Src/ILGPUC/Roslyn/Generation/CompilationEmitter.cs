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
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

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

        // Debug information requires the encoding of every source text
        foreach (var tree in compilation.SyntaxTrees)
        {
            var text = tree.GetText();
            if (text.Encoding is null)
            {
                compilation = compilation.ReplaceSyntaxTree(
                    tree,
                    tree.WithChangedText(
                        SourceText.From(text.ToString(), Encoding.UTF8)));
            }
        }

        // Portable PDBs carry the source locations of kernel code, which the
        // kernel compiler reports errors at
        var result = compilation.Emit(
            peStream,
            pdbStream,
            options: new EmitOptions(
                debugInformationFormat: DebugInformationFormat.PortablePdb));

        assemblyBytes = result.Success ? peStream.ToArray() : null;
        pdbBytes = result.Success ? pdbStream.ToArray() : null;

        return result;
    }
}
