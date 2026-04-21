// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IDumpable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;

namespace ILGPUC.IR;

/// <summary>
/// A dumpable IR object for debugging purposes.
/// </summary>
interface IDumpable
{
    /// <summary>
    /// Dumps this object to the given text writer using the default printer
    /// (<see cref="IRPrinterFormat.ILGPU"/>) and normalized names.
    /// </summary>
    void Dump(TextWriter textWriter);

    /// <summary>
    /// Dumps this object to the given text writer using the specified dump mode
    /// and pipeline point. Simple implementors inherit a default that delegates
    /// to <see cref="Dump(TextWriter)"/>.
    /// </summary>
    void Dump(
        TextWriter textWriter,
        IRDumpMode mode,
        IRDumpPoint point = IRDumpPoint.None) =>
        Dump(textWriter);

    /// <summary>
    /// Dumps this object to the given text writer using the specified dump mode,
    /// printer format, and pipeline point. Simple implementors inherit a default
    /// that ignores the format and delegates to
    /// <see cref="Dump(TextWriter, IRDumpMode, IRDumpPoint)"/>.
    /// </summary>
    void Dump(
        TextWriter textWriter,
        IRDumpMode mode,
        IRPrinterFormat format,
        IRDumpPoint point = IRDumpPoint.None) =>
        Dump(textWriter, mode, point);
}

/// <summary>
/// Selects which printer backend is used when dumping IR text.
/// </summary>
enum IRPrinterFormat
{
    /// <summary>
    /// ILGPU-native format: block-prefixed value names
    /// (<c>%entry.0</c>, <c>%bb1.phi0</c>), ILGPU opcode namespaces
    /// (<c>arith.*</c>, <c>mem.*</c>, <c>gpu.*</c>, …), and the module
    /// generation index in the header.
    /// </summary>
    ILGPU,

    /// <summary>
    /// LLVM IR style: sequential value numbers (<c>%0</c>, <c>%1</c>),
    /// standard LLVM mnemonics (<c>add</c>, <c>icmp</c>, <c>getelementptr</c>),
    /// and GPU extensions emitted as <c>@ilgpu.*</c> intrinsic calls.
    /// </summary>
    LLVM,
}

/// <summary>
/// Controls whether output uses raw internal IDs or stable canonical names.
/// </summary>
enum IRDumpMode
{
    /// <summary>
    /// Real internal value IDs (<c>%_42</c>, <c>b_17</c>) for low-level
    /// debugging.
    /// </summary>
    Raw,

    /// <summary>
    /// Stable canonical names with no internal IDs
    /// (<c>%0</c>/<c>%entry.0</c>, <c>entry</c>, <c>bb1</c>).
    /// </summary>
    Normalized,
}

/// <summary>
/// Names the pipeline stage at which an IR dump is taken.
/// </summary>
[Flags]
enum IRDumpPoint
{
    /// <summary>
    /// No dump point selected.
    /// </summary>
    None = 0,

    /// <summary>
    /// IL → IR done; no passes run yet.
    /// </summary>
    AfterFrontend = 1 << 0,

    /// <summary>
    /// Backend-agnostic passes complete.
    /// </summary>
    AfterGlobalOpt = 1 << 1,

    /// <summary>
    /// Backend-specific lowering complete.
    /// </summary>
    AfterBackendTransforms = 1 << 2,

    /// <summary>
    /// All dump points enabled.
    /// </summary>
    All = AfterFrontend | AfterGlobalOpt | AfterBackendTransforms,
}

/// <summary>
/// Extension methods for <see cref="IDumpable"/> instances.
/// </summary>
static class Dumpable
{
    /// <summary>
    /// Dumps the IR object to the console output using the specified mode and
    /// printer format.
    /// </summary>
    public static void DumpToConsole(
        this IDumpable dumpable,
        IRDumpMode mode = IRDumpMode.Raw,
        IRPrinterFormat format = IRPrinterFormat.ILGPU,
        IRDumpPoint point = IRDumpPoint.None) =>
        dumpable.Dump(Console.Out, mode, format, point);

    /// <summary>
    /// Dumps the IR object to the console error output using normalized mode
    /// and the default printer format.
    /// </summary>
    public static void DumpToError(this IDumpable dumpable) =>
        dumpable.Dump(Console.Error, IRDumpMode.Normalized);

    /// <summary>
    /// Dumps the IR object to the console error output using the specified mode
    /// and printer format.
    /// </summary>
    public static void DumpToError(
        this IDumpable dumpable,
        IRDumpMode mode,
        IRPrinterFormat format = IRPrinterFormat.ILGPU,
        IRDumpPoint point = IRDumpPoint.None) =>
        dumpable.Dump(Console.Error, mode, format, point);

    /// <summary>
    /// Dumps the IR object to a file using normalized mode and the default
    /// printer format.
    /// </summary>
    public static void DumpToFile(this IDumpable dumpable, string fileName)
    {
        using var stream = new StreamWriter(fileName, false);
        dumpable.Dump(stream, IRDumpMode.Normalized);
    }

    /// <summary>
    /// Dumps the IR object to a file using the specified mode and printer format.
    /// </summary>
    public static void DumpToFile(
        this IDumpable dumpable,
        string fileName,
        IRDumpMode mode,
        IRPrinterFormat format = IRPrinterFormat.ILGPU,
        IRDumpPoint point = IRDumpPoint.None)
    {
        using var stream = new StreamWriter(fileName, false);
        dumpable.Dump(stream, mode, format, point);
    }
}
