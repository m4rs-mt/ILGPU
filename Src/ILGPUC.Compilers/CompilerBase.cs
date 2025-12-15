// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ILGPUC.Compilers;

/// <summary>
/// Abstract base class for all GPU compiler wrappers. Provides shared helpers for
/// launching compiler processes, managing temporary directories, encoding output
/// files, and sanitizing user-supplied arguments.
/// </summary>
public abstract partial class CompilerBase : ICompiler
{
    private readonly string _executablePath;
    private readonly Lazy<bool> _isAvailable;
    private ToolchainComponent[]? _toolchainComponents;

    /// <summary>
    /// Initializes a new instance of <see cref="CompilerBase"/> with the given
    /// compiler executable path.
    /// </summary>
    /// <param name="executablePath">
    /// The absolute path to the compiler executable
    /// (e.g. <c>/usr/local/cuda/bin/nvcc</c>).
    /// </param>
    protected CompilerBase(string executablePath)
    {
        _executablePath = executablePath;
        _isAvailable = new Lazy<bool>(() => CheckAvailability());
    }

    /// <inheritdoc/>
    public abstract CompilationTarget Target { get; }

    /// <inheritdoc/>
    public bool IsAvailable => _isAvailable.Value;

    /// <inheritdoc/>
    public abstract Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct);

    /// <summary>
    /// Returns the first line of the compiler's <c>--version</c> output, or
    /// <c>null</c> if the compiler is unavailable or the invocation fails.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A short version string (e.g. <c>"nvcc: NVIDIA (R) Cuda compiler driver …"</c>),
    /// or <c>null</c>.
    /// </returns>
    public virtual async Task<string?> GetVersionAsync(CancellationToken ct)
    {
        if (!IsAvailable)
            return null;

        try
        {
            var (_, stdout, _) = await RunProcessAsync(
                _executablePath,
                "--version",
                workingDirectory: null,
                ct).ConfigureAwait(false);
            return stdout.Trim().Split('\n')[0];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the per-component toolchain health status. Accessing this property forces
    /// the lazy availability check to run if it hasn't already.
    /// </summary>
    public ToolchainComponent[] ToolchainComponents
    {
        get
        {
            // Force the lazy init so _toolchainComponents is populated.
            _ = _isAvailable.Value;
            return _toolchainComponents ?? [];
        }
    }

    /// <summary>
    /// Gets the absolute path to the compiler executable.
    /// </summary>
    protected string ExecutablePath => _executablePath;

    /// <summary>
    /// Creates a failed <see cref="CompilationResult"/> with the given diagnostic output.
    /// </summary>
    protected CompilationResult Failure(
        string stdout, string stderr, int exitCode, OutputType outputType) =>
        new()
        {
            Success = false,
            StdOut = stdout,
            StdErr = stderr,
            ExitCode = exitCode,
            Target = Target,
            OutputType = outputType,
        };

    /// <summary>
    /// Launches an external process and captures its stdout and stderr.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The command-line arguments to pass.</param>
    /// <param name="workingDirectory">
    /// The working directory for the process, or <c>null</c> to inherit the current one.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A tuple of (<paramref name="fileName"/>'s exit code, stdout text, stderr text).
    /// </returns>
    protected static async Task<(int exitCode, string stdout, string stderr)>
        RunProcessAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (process.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Creates a uniquely named temporary directory under the system temp path.
    /// </summary>
    /// <returns>The absolute path of the newly created directory.</returns>
    protected static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"gpu-compilation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Deletes a temporary directory on a best-effort basis; any exceptions are silently
    /// swallowed to avoid masking compilation errors.
    /// </summary>
    /// <param name="path">The absolute path of the directory to remove.</param>
    protected static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    /// <summary>
    /// Reads a file and returns its contents as a base-64-encoded string.
    /// </summary>
    /// <param name="filePath">The absolute path of the file to encode.</param>
    /// <returns>The base-64 representation of the file's raw bytes.</returns>
    protected static string Base64EncodeFile(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Builds the <c>-I"…"</c> include-path arguments from the request.
    /// </summary>
    /// <param name="request">
    /// The compile request containing optional include paths.
    /// </param>
    /// <returns>
    /// A string of space-separated <c>-I"path"</c> flags, or an empty string if none.
    /// </returns>
    protected static string BuildIncludeArgs(CompileRequest request)
    {
        if (request.IncludePaths is not { Length: > 0 })
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var path in request.IncludePaths)
        {
            var sanitized = SanitizeArgument(path);
            sb.Append($" -I\"{sanitized}\"");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the <c>-D"…"</c> preprocessor-directive arguments from the request.
    /// </summary>
    /// <param name="request">
    /// The compile request containing optional compiler directives.
    /// </param>
    /// <returns>
    /// A string of space-separated <c>-D"directive"</c> flags, or an empty string if
    /// none.
    /// </returns>
    protected static string BuildDirectiveArgs(CompileRequest request)
    {
        if (request.CompilerDirectives is not { Length: > 0 })
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var directive in request.CompilerDirectives)
        {
            var sanitized = SanitizeArgument(directive);
            sb.Append($" -D\"{sanitized}\"");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates and returns user-supplied compiler flags, rejecting any that contain
    /// shell metacharacters that could enable command injection.
    /// </summary>
    /// <param name="flags">The raw flags string from the compile request.</param>
    /// <returns>
    /// The original <paramref name="flags"/> value if it is safe, or an empty string if
    /// <paramref name="flags"/> is <c>null</c> or whitespace.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="flags"/> contains disallowed characters.
    /// </exception>
    protected static string SanitizeCompilerFlags(string? flags)
    {
        if (string.IsNullOrWhiteSpace(flags))
            return string.Empty;

        // Reject flags containing shell metacharacters
        if (DangerousCharsRegex().IsMatch(flags))
            throw new ArgumentException("Compiler flags contain disallowed characters");

        return flags;
    }

    [GeneratedRegex(@"[;|&`$\(\){}!<>]")]
    private static partial Regex DangerousCharsRegex();

    private static string SanitizeArgument(string value)
    {
        // Remove embedded quotes and backslashes that could break out of quoting
        return value.Replace("\"", "", StringComparison.Ordinal)
                    .Replace("\\", "", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks a single toolchain component by launching it with the given arguments
    /// and capturing the first line of stdout as a version string.
    /// </summary>
    /// <param name="name">A short display name for the component (e.g. <c>"ptxas"</c>).</param>
    /// <param name="fileName">The executable to launch.</param>
    /// <param name="arguments">The arguments to pass (defaults to <c>"--version"</c>).</param>
    /// <param name="timeoutSeconds">Maximum seconds to wait for the process.</param>
    /// <returns>A <see cref="ToolchainComponent"/> describing the result.</returns>
    protected static ToolchainComponent CheckTool(
        string name,
        string fileName,
        string arguments = "--version",
        int timeoutSeconds = 5)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds));

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                return new ToolchainComponent
                {
                    Name = name,
                    Available = false,
                    Error = string.IsNullOrWhiteSpace(stderr)
                        ? $"Exited with code {process.ExitCode}"
                        : stderr.Trim().Split('\n')[0],
                };
            }

            var version = stdout.Trim().Split('\n')[0];
            return new ToolchainComponent
            {
                Name = name,
                Available = true,
                Version = string.IsNullOrWhiteSpace(version) ? null : version,
            };
        }
        catch (Exception ex)
        {
            return new ToolchainComponent
            {
                Name = name,
                Available = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Returns the set of toolchain components to check. The base implementation
    /// checks only the primary executable. Derived classes can override to add
    /// additional tool checks.
    /// </summary>
    /// <returns>An array of <see cref="ToolchainComponent"/> results.</returns>
    protected virtual ToolchainComponent[] GetToolchainComponents()
    {
        return [CheckTool(Path.GetFileName(_executablePath), _executablePath)];
    }

    /// <summary>
    /// Runs all toolchain component checks and returns <c>true</c> only if every
    /// component is available.
    /// </summary>
    private bool CheckAvailability()
    {
        _toolchainComponents = GetToolchainComponents();
        return _toolchainComponents.All(c => c.Available);
    }
}
