// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ManifestWriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// Writes rewritten and generated source files to disk,
/// and produces a manifest listing which original files were replaced.
/// </summary>
sealed class ManifestWriter(string outputDir)
{
    private readonly string _rewrittenDir = Path.Combine(outputDir, "rewritten");
    private readonly string _generatedDir = Path.Combine(outputDir, "generated");

    /// <summary>
    /// Writes all output files and the manifest.
    /// </summary>
    /// <param name="rewrittenFiles">
    /// Map of original file path -> rewritten syntax tree.
    /// Only files that were actually modified should be included.
    /// </param>
    /// <param name="generatedSources">
    /// List of (filename, source) pairs for generated files
    /// (CompiledKernel classes, registrar, etc.).
    /// </param>
    /// <param name="projectDir">
    /// The project root directory, used to compute relative paths
    /// for rewritten files. If null, file names are used as-is.
    /// </param>
    public void WriteAll(
        Dictionary<string, SyntaxTree> rewrittenFiles,
        List<(string FileName, string Source)> generatedSources,
        string? projectDir = null)
    {
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(_rewrittenDir);
        Directory.CreateDirectory(_generatedDir);

        var manifestLines = new List<string>();

        // Write rewritten files
        foreach (var (originalPath, tree) in rewrittenFiles)
        {
            var relativePath = ComputeRelativePath(originalPath, projectDir);
            var outputPath = Path.Combine(_rewrittenDir, relativePath);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, tree.GetRoot().ToFullString());
            manifestLines.Add(originalPath);
        }

        // Write generated files
        foreach (var (fileName, source) in generatedSources)
        {
            var outputPath = Path.Combine(_generatedDir, fileName);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, source);
        }

        // Write manifest (one original path per line that was rewritten)
        var manifestPath = Path.Combine(outputDir, "manifest.txt");
        File.WriteAllLines(manifestPath, manifestLines);
    }

    /// <summary>
    /// Computes the output path for a rewritten file relative to
    /// <paramref name="projectDir"/>. If <paramref name="projectDir"/> is
    /// <see langword="null"/> or the file is outside the project directory,
    /// returns the file name only.
    /// </summary>
    private static string ComputeRelativePath(string filePath, string? projectDir)
    {
        if (projectDir == null)
            return Path.GetFileName(filePath);

        // Normalize paths
        var fullFile = Path.GetFullPath(filePath);
        var fullProject = Path.GetFullPath(projectDir);
        if (!fullProject.EndsWith(Path.DirectorySeparatorChar))
            fullProject += Path.DirectorySeparatorChar;

        if (fullFile.StartsWith(fullProject, StringComparison.OrdinalIgnoreCase))
            return fullFile[fullProject.Length..];

        return Path.GetFileName(filePath);
    }
}
