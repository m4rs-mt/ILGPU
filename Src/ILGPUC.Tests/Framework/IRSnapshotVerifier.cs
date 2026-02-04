// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRSnapshotVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Compares normalized IR output against reference .il snapshot files.
/// When ILGPU_UPDATE_IR=1 is set, writes actual IR to the source-tree Snapshots/ dir.
/// </summary>
static class IRSnapshotVerifier
{
    private static readonly bool s_updateMode =
        Environment.GetEnvironmentVariable("ILGPU_UPDATE_IR") == "1";

    /// <summary>
    /// Verifies or updates a snapshot file.
    /// </summary>
    /// <param name="actualIR">The normalized IR text to verify.</param>
    /// <param name="snapshotRelativePath">
    /// Path relative to Snapshots/ directory, e.g.
    /// AfterFrontend/BasicIfKernels.IfTrueKernel.il"
    /// </param>
    /// <param name="callerFilePath">Auto-populated by compiler.</param>
    public static void VerifyOrUpdate(
        string actualIR,
        string snapshotRelativePath,
        [CallerFilePath] string callerFilePath = "")
    {
        if (s_updateMode)
        {
            UpdateSnapshot(actualIR, snapshotRelativePath, callerFilePath);
        }
        else
        {
            VerifySnapshot(actualIR, snapshotRelativePath);
        }
    }

    /// <summary>
    /// Builds the snapshot relative path for a given dump point.
    /// </summary>
    public static string GetSnapshotPath(
        string className,
        string methodName,
        ILGPUC.IR.IRDumpPoint point,
        OptimizationLevel? optLevel = null,
        ILGPUC.Backends.BackendType? backend = null,
        CompilationMode? mode = null)
    {
        var optSuffix = optLevel.HasValue ? $".{optLevel.Value}" : "";
        var modeSuffix = mode.HasValue ? $".{mode.Value}" : "";

        return point switch
        {
            ILGPUC.IR.IRDumpPoint.AfterFrontend =>
                Path.Combine("AfterFrontend", $"{className}.{methodName}.il"),
            ILGPUC.IR.IRDumpPoint.AfterGlobalOpt =>
                Path.Combine("AfterGlobalOpt",
                    $"{className}.{methodName}{optSuffix}{modeSuffix}.il"),
            ILGPUC.IR.IRDumpPoint.AfterBackendTransforms =>
                Path.Combine(
                    "AfterBackendTransforms",
                    backend?.ToString() ?? "CPU",
                    $"{className}.{methodName}{optSuffix}{modeSuffix}.il"),
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    }

    private static void VerifySnapshot(string actualIR, string snapshotRelativePath)
    {
        // Read from build output (CopyToOutputDirectory puts it next to the assembly)
        var snapshotPath = Path.Combine(
            AppContext.BaseDirectory,
            "Snapshots",
            snapshotRelativePath);

        if (!File.Exists(snapshotPath))
        {
            // If the Snapshots dir is empty, the submodule is not initialized.
            var snapshotsRoot = Path.Combine(AppContext.BaseDirectory, "Snapshots");
            if (!Directory.Exists(snapshotsRoot) ||
                !Directory.EnumerateFiles(
                    snapshotsRoot,
                    "*.il",
                    SearchOption.AllDirectories).Any())
            {
                throw new SkipException(
                    "Snapshot submodule not initialized. Run: " +
                    "git submodule update --init Src/ILGPUC.Tests/Snapshots");
            }

            Assert.Fail(
                $"Snapshot file not found: {snapshotRelativePath}. " +
                "Run with ILGPU_UPDATE_IR=1 to generate snapshots.");
        }

        var expected = File.ReadAllText(snapshotPath);
        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actualIR));
    }

    private static void UpdateSnapshot(
        string actualIR,
        string snapshotRelativePath,
        string callerFilePath)
    {
        // Navigate from the caller's file to the project root's Snapshots/ dir
        var projectRoot = FindProjectRoot(callerFilePath);
        var snapshotPath = Path.Combine(projectRoot, "Snapshots", snapshotRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        File.WriteAllText(snapshotPath, actualIR);
    }

    private static string FindProjectRoot(string callerFilePath)
    {
        var dir = Path.GetDirectoryName(callerFilePath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ILGPUC.Tests.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: assume callerFilePath is in a subdirectory of the project
        return Path.GetDirectoryName(callerFilePath)!;
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").TrimEnd();
}
