// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntegrationProjectFixture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Per-test fixture that materializes a template project from
/// <c>IntegrationProjects/&lt;Name&gt;/</c> into a fresh temp working directory,
/// substitutes <c>$(ILGPU_REPO_ROOT)</c>, <c>$(ILGPU_BACKEND)</c>, and
/// <c>$(ILGPUC_TEMP_FEED)</c> tokens in any <c>*.template</c> files, and
/// exposes helpers for driving <c>dotnet build/clean</c> and running the
/// resulting binary.
/// </summary>
/// <remarks>
/// On <see cref="Dispose"/>, the temp dir is deleted UNLESS the test threw or
/// <c>ILGPUC_KEEP_TEMP=1</c> is set in the environment. The path is always
/// echoed to <see cref="ITestOutputHelper"/> so failing tests can be inspected
/// post-mortem (response file, manifest, rewritten/, generated/).
/// </remarks>
sealed class IntegrationProjectFixture : IDisposable
{
    private static readonly Lazy<string> s_repoRoot = new(FindRepoRoot);

    private readonly string _projectName;
    private readonly ITestOutputHelper _output;
    private bool _failed;

    /// <summary>Absolute path to the temp working directory.</summary>
    public string WorkingDir { get; }

    /// <summary>Absolute path to the materialized .csproj.</summary>
    public string ProjectPath { get; }

    /// <summary>Backend the project was configured for.</summary>
    public BackendType Backend { get; }

    /// <summary>Repo root containing <c>Src/ILGPU.sln</c>.</summary>
    public static string RepoRoot => s_repoRoot.Value;

    /// <summary>
    /// Resolved path to the locally built <c>ILGPUC.dll</c>. The fixture
    /// fails fast at construction if this file is missing.
    /// </summary>
    public static string IlgpucDllPath { get; } = Path.Combine(
        RepoRoot, "Bin", "Debug", "net10.0", "ILGPUC.dll");

    /// <summary>
    /// Resolved path to the locally built <c>ILGPU.dll</c>.
    /// </summary>
    public static string IlgpuDllPath { get; } = Path.Combine(
        RepoRoot, "Bin", "Debug", "net10.0", "ILGPU.dll");

    public IntegrationProjectFixture(
        string projectName,
        BackendType backend,
        ITestOutputHelper output,
        IReadOnlyDictionary<string, string>? extraTokens = null)
    {
        _projectName = projectName;
        Backend = backend;
        _output = output;

        // Fail fast if ILGPUC isn't built.
        if (!File.Exists(IlgpucDllPath))
        {
            throw new FileNotFoundException(
                $"ILGPUC.dll not found at {IlgpucDllPath}. " +
                $"Run `dotnet build ILGPU.sln` before running integration tests.",
                IlgpucDllPath);
        }
        if (!File.Exists(IlgpuDllPath))
        {
            throw new FileNotFoundException(
                $"ILGPU.dll not found at {IlgpuDllPath}. " +
                $"Run `dotnet build ILGPU.sln` before running integration tests.",
                IlgpuDllPath);
        }

        var templateDir = Path.Combine(
            AppContext.BaseDirectory, "IntegrationProjects", projectName);
        if (!Directory.Exists(templateDir))
        {
            throw new DirectoryNotFoundException(
                $"Integration project template not found: {templateDir}. " +
                $"Check the IntegrationProjects/ asset copy rule in " +
                $"ILGPUC.Tests.csproj.");
        }

        WorkingDir = Path.Combine(
            Path.GetTempPath(), $"ilgpuc_msbuild_{Guid.NewGuid():N}");
        Directory.CreateDirectory(WorkingDir);
        _output.WriteLine($"Integration working dir: {WorkingDir}");

        // Build the token table.
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["$(ILGPU_REPO_ROOT)"] = NormalizePath(RepoRoot),
            ["$(ILGPU_BACKEND)"] = backend.ToString(),
            // PREFER_CPU is substituted into Program.cs.template's call to
            // GetPreferredDevice. CPU theory cases need preferCPU: true; GPU
            // backends (Metal etc.) need preferCPU: false so the runtime
            // resolves a matching accelerator.
            ["$(PREFER_CPU)"] = backend == BackendType.CPU ? "true" : "false",
        };
        if (extraTokens is not null)
        {
            foreach (var kvp in extraTokens)
                tokens[kvp.Key] = kvp.Value;
        }

        // Copy template directory, materializing *.template files into their
        // non-suffixed counterparts with token substitution.
        ProjectPath = string.Empty;
        foreach (var srcFile in Directory.EnumerateFiles(
                     templateDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(templateDir, srcFile);
            var destRelative = relative.EndsWith(
                ".template", StringComparison.Ordinal)
                ? relative[..^".template".Length]
                : relative;
            var destFile = Path.Combine(WorkingDir, destRelative);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destFile)!);

            if (relative.EndsWith(".template", StringComparison.Ordinal))
            {
                var content = File.ReadAllText(srcFile);
                foreach (var (token, value) in tokens)
                    content = content.Replace(token, value, StringComparison.Ordinal);
                File.WriteAllText(destFile, content);
            }
            else
            {
                File.Copy(srcFile, destFile, overwrite: true);
            }

            if (destRelative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                ProjectPath = destFile;
        }

        if (string.IsNullOrEmpty(ProjectPath))
        {
            throw new InvalidOperationException(
                $"No .csproj found in template '{projectName}'. Make sure the " +
                $"template directory contains a *.csproj or *.csproj.template file.");
        }
    }

    /// <summary>
    /// Run <c>dotnet build</c> on the materialized project. Optionally pass
    /// extra MSBuild properties (e.g. <c>ILGPUBackend</c>).
    /// </summary>
    public async Task<MsBuildResult> BuildAsync(
        IDictionary<string, string>? extraProperties = null)
    {
        var result = await MsBuildRunner.BuildAsync(
            ProjectPath, properties: extraProperties).ConfigureAwait(false);
        LogResult("build", result);
        return result;
    }

    /// <summary>Run <c>dotnet clean</c> on the materialized project.</summary>
    public async Task<MsBuildResult> CleanAsync()
    {
        var result = await MsBuildRunner.CleanAsync(ProjectPath).ConfigureAwait(false);
        LogResult("clean", result);
        return result;
    }

    /// <summary>Run <c>dotnet restore</c> on the materialized project.</summary>
    public async Task<MsBuildResult> RestoreAsync(
        IDictionary<string, string>? extraProperties = null)
    {
        var result = await MsBuildRunner.RestoreAsync(
            ProjectPath, extraProperties).ConfigureAwait(false);
        LogResult("restore", result);
        return result;
    }

    /// <summary>
    /// Run the resulting binary via <see cref="ProcessRunner"/> and return its
    /// stdout/stderr/exit code. Assumes the project has already been built.
    /// </summary>
    public async Task<ProcessResult> RunAsync()
    {
        var dllPath = ResolveBuiltDll();
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException(
                $"Built binary not found at {dllPath}. Did the build succeed?",
                dllPath);
        }

        var result = await ProcessRunner.RunAsync(dllPath).ConfigureAwait(false);
        _output.WriteLine($"Run exit code: {result.ExitCode}, " +
                          $"timed out: {result.TimedOut}");
        if (result.StdOutLines.Length > 0)
        {
            _output.WriteLine("Run stdout:");
            foreach (var line in result.StdOutLines)
                _output.WriteLine($"  {line}");
        }
        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            _output.WriteLine("Run stderr:");
            _output.WriteLine(result.StdErr);
        }
        return result;
    }

    /// <summary>
    /// Path to the per-project <c>obj/Debug/net10.0/ilgpu/</c> directory
    /// where the .targets file deposits manifest.txt, args.rsp, rewritten/,
    /// and generated/.
    /// </summary>
    public string GetIlgpuOutputDir() => Path.Combine(
        WorkingDir, "obj", "Debug", "net10.0", "ilgpu");

    public string ManifestPath => Path.Combine(GetIlgpuOutputDir(), "manifest.txt");

    public string ResponseFilePath => Path.Combine(GetIlgpuOutputDir(), "args.rsp");

    public string GeneratedDir => Path.Combine(GetIlgpuOutputDir(), "generated");

    public string RewrittenDir => Path.Combine(GetIlgpuOutputDir(), "rewritten");

    public string ReadManifest() =>
        File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : string.Empty;

    public string ReadResponseFile() =>
        File.Exists(ResponseFilePath) ? File.ReadAllText(ResponseFilePath) : string.Empty;

    public string[] ListGenerated() =>
        Directory.Exists(GeneratedDir)
            ? Directory.GetFiles(GeneratedDir, "*.cs", SearchOption.AllDirectories)
                .Select(p => Path.GetFileName(p))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray()
            : [];

    public string[] ListRewritten() =>
        Directory.Exists(RewrittenDir)
            ? Directory.GetFiles(RewrittenDir, "*.cs", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(RewrittenDir, p))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray()
            : [];

    /// <summary>Resolve the path of the built executable .dll.</summary>
    public string ResolveBuiltDll() => Path.Combine(
        WorkingDir, "bin", "Debug", "net10.0", $"{_projectName}.dll");

    /// <summary>
    /// Mark the fixture as failed so the temp directory is preserved on
    /// dispose for forensics.
    /// </summary>
    public void MarkFailed() => _failed = true;

    public void Dispose()
    {
        var keepEnv = Environment.GetEnvironmentVariable("ILGPUC_KEEP_TEMP");
        var keep = _failed
            || (!string.IsNullOrEmpty(keepEnv) && keepEnv != "0");

        if (keep)
        {
            _output.WriteLine(
                $"Preserving integration working dir for inspection: {WorkingDir}");
            return;
        }

        try
        {
            if (Directory.Exists(WorkingDir))
                Directory.Delete(WorkingDir, recursive: true);
        }
        catch (Exception ex)
        {
            _output.WriteLine(
                $"Warning: failed to delete temp dir {WorkingDir}: {ex.Message}");
        }
    }

    private void LogResult(string operation, MsBuildResult result)
    {
        _output.WriteLine(
            $"dotnet {operation}: exit={result.ExitCode}, " +
            $"timed_out={result.TimedOut}, lines={result.StdOutLines.Length}");
        if (!result.Succeeded)
        {
            _output.WriteLine($"--- {operation} stdout ---");
            foreach (var line in result.StdOutLines)
                _output.WriteLine(line);
            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                _output.WriteLine($"--- {operation} stderr ---");
                _output.WriteLine(result.StdErr);
            }
        }
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Src", "ILGPU.sln");
            if (File.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate ILGPU repo root from {AppContext.BaseDirectory}. " +
            $"Expected to find Src/ILGPU.sln in some ancestor directory.");
    }
}
