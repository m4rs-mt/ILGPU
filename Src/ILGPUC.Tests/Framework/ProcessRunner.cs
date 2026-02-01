// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ProcessRunner.cs
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Result of running a process.
/// </summary>
public sealed record ProcessResult(
    int ExitCode,
    string[] StdOutLines,
    string StdErr,
    bool TimedOut);

/// <summary>
/// Runs an executable as a separate process with timeout and stdout capture.
/// </summary>
static class ProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs a .NET DLL assembly via <c>dotnet</c> with stdout/stderr capture.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string dllPath,
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        if (timeout == default)
            timeout = DefaultTimeout;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

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

        // Parse stdout into lines, trimming trailing empty lines
        var stdout = stdoutBuilder.ToString();
        var lines = stdout.Split(
            ["\r\n", "\n"],
            StringSplitOptions.None);

        // Trim trailing empty lines
        int end = lines.Length;
        while (end > 0 && string.IsNullOrEmpty(lines[end - 1]))
            end--;
        var trimmedLines = lines[..end];

        return new ProcessResult(
            timedOut ? -1 : process.ExitCode,
            trimmedLines,
            stderrBuilder.ToString(),
            timedOut);
    }
}
