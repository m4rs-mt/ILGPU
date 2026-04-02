// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MsBuildRunner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Result of a <c>dotnet</c> sub-command invocation.
/// </summary>
public sealed record MsBuildResult(
    int ExitCode,
    string[] StdOutLines,
    string StdErr,
    bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;

    public string FullStdOut => string.Join("\n", StdOutLines);
}

/// <summary>
/// Wraps <c>dotnet build</c> / <c>clean</c> / <c>pack</c> / <c>restore</c> as
/// a subprocess so MSBuild integration tests can drive a real build of a
/// standalone .csproj.
/// </summary>
static class MsBuildRunner
{
    // 15 minutes covers cold restores, full graph rebuilds, and slow CI
    // hosts. Tests rarely take this long; the timeout exists so the runner
    // doesn't hang the test process forever on a stuck dotnet child.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(900);

    public static Task<MsBuildResult> BuildAsync(
        string projectPath,
        string configuration = "Debug",
        IDictionary<string, string>? properties = null,
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "build",
            $"\"{projectPath}\"",
            "-c", configuration,
            "--nologo",
            "-v:normal",
        };
        AppendProperties(args, properties);
        return RunDotnetAsync(args, projectPath, timeout, ct);
    }

    public static Task<MsBuildResult> CleanAsync(
        string projectPath,
        string configuration = "Debug",
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "clean",
            $"\"{projectPath}\"",
            "-c", configuration,
            "--nologo",
        };
        return RunDotnetAsync(args, projectPath, timeout, ct);
    }

    public static Task<MsBuildResult> RestoreAsync(
        string projectPath,
        IDictionary<string, string>? properties = null,
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "restore",
            $"\"{projectPath}\"",
            "--nologo",
        };
        AppendProperties(args, properties);
        return RunDotnetAsync(args, projectPath, timeout, ct);
    }

    public static Task<MsBuildResult> PackAsync(
        string projectPath,
        string outputDir,
        string configuration = "Release",
        bool noBuild = false,
        IDictionary<string, string>? properties = null,
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "pack",
            $"\"{projectPath}\"",
            "-c", configuration,
            "-o", $"\"{outputDir}\"",
            "--nologo",
        };
        if (noBuild)
            args.Add("--no-build");
        AppendProperties(args, properties);
        return RunDotnetAsync(args, projectPath, timeout, ct);
    }

    private static void AppendProperties(
        List<string> args, IDictionary<string, string>? properties)
    {
        if (properties is null)
            return;
        foreach (var kvp in properties)
            args.Add($"-p:{kvp.Key}={kvp.Value}");
    }

    private static async Task<MsBuildResult> RunDotnetAsync(
        List<string> args,
        string projectPath,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (timeout == default)
            timeout = DefaultTimeout;

        var workingDir = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? Directory.GetCurrentDirectory();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(' ', args),
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Suppress NuGet/restore noise that bleats about missing tool manifests etc.
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        // Disable persistent MSBuild / Roslyn worker processes. Without this,
        // `dotnet pack` (and sometimes `dotnet build`) spawns a long-lived
        // child that inherits the redirected stdout/stderr handles and holds
        // them open AFTER the parent exits — making `WaitForExitAsync` block
        // for the full timeout even though the actual work finished in
        // milliseconds.
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["UseSharedCompilation"] = "false";

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            timedOut = true;
            try { process.Kill(entireProcessTree: true); }
            catch { /* best effort */ }
        }

        var stdout = stdoutBuilder.ToString();
        var lines = stdout.Split(["\r\n", "\n"], StringSplitOptions.None);
        int end = lines.Length;
        while (end > 0 && string.IsNullOrEmpty(lines[end - 1]))
            end--;
        var trimmedLines = lines[..end];

        return new MsBuildResult(
            timedOut ? -1 : process.ExitCode,
            trimmedLines,
            stderrBuilder.ToString(),
            timedOut);
    }
}
