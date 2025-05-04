// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXDebugInfoGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System.Collections.Generic;
using System.Text;

namespace ILGPUC.Backends.PTX;

/// <summary>
/// Represents a debug information scope.
/// </summary>
sealed class PTXDebugInfoGeneratorScope(PTXDebugInfoGenerator parent)
{
    /// <summary>
    /// Returns the current location.
    /// </summary>
    public FileLocation? Current { get; private set; }

    /// <summary>
    /// Generates debug information for the given node.
    /// </summary>
    /// <param name="builder">The target string builder to write to.</param>
    /// <param name="node">The node.</param>
    public void GenerateDebugInfo(StringBuilder builder, Node node)
    {
        if (node.Location is not FileLocation location || Current == location)
            return;
        parent.GenerateDebugInfo(builder, node, location);
        Current = location;
    }

    /// <summary>
    /// Reset all location information.
    /// </summary>
    public void ResetLocation() => Current = null;
}

/// <summary>
/// A general debug info generator for PTX kernels.
/// </summary>
abstract class PTXDebugInfoGenerator
{
    /// <summary>
    /// Constructs a new generic debug info generator.
    /// </summary>
    protected PTXDebugInfoGenerator() { }

    /// <summary>
    /// Begins a new debug information scope.
    /// </summary>
    public PTXDebugInfoGeneratorScope BeginScope() => new(this);

    /// <summary>
    /// Generates debug information for the given node.
    /// </summary>
    /// <param name="builder">The target string builder to write to.</param>
    /// <param name="node">The node.</param>
    /// <param name="location">The current location.</param>
    protected internal abstract void GenerateDebugInfo(
        StringBuilder builder,
        Node node,
        FileLocation location);

    /// <summary>
    /// Generate required debug-information sections in PTX code.
    /// </summary>
    /// <param name="builder">The target string builder to write to.</param>
    public abstract void GenerateDebugSections(StringBuilder builder);
}

/// <summary>
/// Represents an info generator that does not generate anything.
/// </summary>
sealed class PTXNoDebugInfoGenerator : PTXDebugInfoGenerator
{
    /// <summary>
    /// An empty debug information generator.
    /// </summary>
    public static readonly PTXNoDebugInfoGenerator Empty = new();

    private PTXNoDebugInfoGenerator() { }

    /// <summary>
    /// Generates no debug information.
    /// </summary>
    protected internal override void GenerateDebugInfo(
        StringBuilder builder,
        Node node,
        FileLocation location)
    { }

    /// <summary>
    /// Generates no debug information section.
    /// </summary>
    public override void GenerateDebugSections(StringBuilder builder) { }
}

/// <summary>
/// Generates line-based debug information for PTX kernels.
/// </summary>
sealed class PTXDebugLineInfoGenerator : PTXDebugInfoGenerator
{
    private readonly Dictionary<string, int> _fileMapping = [];

    /// <summary>
    /// Gets or creates a new file entry.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <returns>The file index.</returns>
    private int RegisterFile(FileLocation location)
    {
        if (!_fileMapping.TryGetValue(
            location.FileName,
            out int fileIndex))
        {
            fileIndex = _fileMapping.Count + 1;
            _fileMapping.Add(location.FileName, fileIndex);
        }
        return fileIndex;
    }

    /// <summary>
    /// Generates a line-based debug information string.
    /// </summary>
    protected internal override void GenerateDebugInfo(
        StringBuilder builder,
        Node node,
        FileLocation location)
    {
        // Register file or reuse an existing index
        int fileIndex = RegisterFile(location);

        // Append a debug annotation for this value
        builder.AppendLine();
        builder.Append("\t.loc\t");
        builder.Append(fileIndex);
        builder.Append(' ');
        builder.Append(location.StartLine);
        builder.Append(' ');
        builder.AppendLine(location.StartColumn.ToString());
    }

    /// <summary>
    /// Generates a debug sections header including file information.
    /// </summary>
    public override void GenerateDebugSections(StringBuilder builder)
    {
        // Append all file declarations
        builder.AppendLine();
        foreach (var fileEntry in _fileMapping)
        {
            builder.Append("\t.file\t");
            builder.Append(fileEntry.Value);
            builder.Append(" \"");
            builder.Append(fileEntry.Key.Replace('\\', '/'));
            builder.AppendLine("\"");
        }

        // Append debug section to enable debugging support
        builder.AppendLine();
        builder.AppendLine(".section.debug_info { }");
    }
}
