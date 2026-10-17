// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HipccCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for AMD HIP, invoking <c>hipcc</c> to produce an object file from
/// HIP source code.
/// </summary>
/// <remarks>
/// Parsing the HIP headers dominates the compile time of a kernel, so the leading
/// <c>#include</c> lines of a source are precompiled once per process and set of
/// flags and passed to every compilation using them with <c>-include-pch</c>.
/// </remarks>
public sealed class HipccCompiler : CompilerBase
{
    private const string DefaultPath = "/opt/rocm/bin/hipcc";

    /// <summary>
    /// The precompiled headers of this process by compiler, flags and headers. A task
    /// result of <see langword="null"/> marks headers that could not be precompiled.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>>
        s_precompiledHeaders = new();

    /// <summary>
    /// The directory holding the precompiled headers of this process, deleted when
    /// the process exits.
    /// </summary>
    private static readonly Lazy<string> s_precompiledHeaderDirectory =
        new(CreatePrecompiledHeaderDirectory);

    /// <summary>
    /// Initializes a new <see cref="HipccCompiler"/> with a custom executable path.
    /// </summary>
    /// <param name="hipccPath">The absolute path to the <c>hipcc</c> executable.</param>
    public HipccCompiler(string hipccPath = DefaultPath)
        : base(hipccPath)
    { }

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.Hip;

    /// <summary>
    /// Compiles the HIP source code in <paramref name="request"/> to an object file and
    /// returns the base-64-encoded output on success.
    /// </summary>
    /// <param name="request">
    /// The compilation request containing source and options.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="CompilationResult"/> describing the outcome.</returns>
    public override async Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var inputFile = Path.Combine(tempDir, "input.hip");
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            // --genco emits a device code object that hipModuleLoadData can
            // load directly; "-c" would produce a host object with the device
            // code embedded, which the module loader rejects. The target GPU
            // architecture (--offload-arch) is passed via ExtraFlags.
            var outputFile = Path.Combine(tempDir, "output.hsaco");
            var optFlag = $"-O{(int)request.OptimizationLevel}";
            var includes = BuildIncludeArgs(request);
            var directives = BuildDirectiveArgs(request);
            var extraFlags = SanitizeCompilerFlags(request.ExtraFlags);
            var flags = $"{optFlag} {extraFlags}{includes}{directives}".Trim();
            var args = $"--genco -o \"{outputFile}\" \"{inputFile}\" {flags}";

            var precompiledHeader = await GetPrecompiledHeaderAsync(
                request.SourceCode,
                flags).ConfigureAwait(false);
            var (exitCode, stdout, stderr) = await RunProcessAsync(
                ExecutablePath,
                precompiledHeader is null
                    ? args
                    : $"{args} -include-pch \"{precompiledHeader}\"",
                tempDir,
                ct).ConfigureAwait(false);

            // Compile without the precompiled header if hipcc rejects it
            if (exitCode != 0 && precompiledHeader is not null &&
                IsPrecompiledHeaderError(stderr))
            {
                (exitCode, stdout, stderr) = await RunProcessAsync(
                    ExecutablePath,
                    args,
                    tempDir,
                    ct).ConfigureAwait(false);
            }

            if (exitCode != 0)
            {
                return new CompilationResult
                {
                    Success = false,
                    StdOut = stdout,
                    StdErr = stderr,
                    ExitCode = exitCode,
                    Target = CompilationTarget.Hip,
                    OutputType = request.OutputType,
                };
            }

            var output = Base64EncodeFile(outputFile);

            return new CompilationResult
            {
                Success = true,
                Output = output,
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = exitCode,
                Target = CompilationTarget.Hip,
                OutputType = request.OutputType,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Returns the precompiled header of the leading <c>#include</c> lines of
    /// <paramref name="sourceCode"/> for the given flags, building it on first use, or
    /// <see langword="null"/> if the source has no such lines or they could not be
    /// precompiled.
    /// </summary>
    /// <param name="sourceCode">The HIP source to compile.</param>
    /// <param name="flags">The optimization, target and preprocessor flags.</param>
    private async Task<string?> GetPrecompiledHeaderAsync(
        string sourceCode,
        string flags)
    {
        var headers = GetLeadingIncludes(sourceCode);
        if (headers.Length < 1)
            return null;

        var key = $"{ExecutablePath}\n{flags}\n{headers}";
        var entry = s_precompiledHeaders.GetOrAdd(
            key,
            k => new Lazy<Task<string?>>(() => BuildPrecompiledHeaderAsync(
                k,
                headers,
                flags)));
        return await entry.Value.ConfigureAwait(false);
    }

    /// <summary>
    /// Precompiles <paramref name="headers"/> with the given flags. The build is not
    /// cancellable, since every later compilation with the same flags shares it.
    /// </summary>
    /// <param name="key">The key identifying the compiler, flags and headers.</param>
    /// <param name="headers">The <c>#include</c> lines to precompile.</param>
    /// <param name="flags">The optimization, target and preprocessor flags.</param>
    /// <returns>The path of the precompiled header, or <see langword="null"/>.</returns>
    private async Task<string?> BuildPrecompiledHeaderAsync(
        string key,
        string headers,
        string flags)
    {
        try
        {
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            var directory = s_precompiledHeaderDirectory.Value;
            var headerFile = Path.Combine(directory, $"{name}.h");
            var outputFile = Path.Combine(directory, $"{name}.pch");
            await File.WriteAllTextAsync(headerFile, headers).ConfigureAwait(false);

            // -S stops hipcc after the device compiler, which then writes the
            // precompiled header instead of handing an object to the linker.
            var (exitCode, _, _) = await RunProcessAsync(
                ExecutablePath,
                $"--genco -x hip -S -Xclang -emit-pch -o \"{outputFile}\" " +
                $"\"{headerFile}\" {flags}",
                directory,
                CancellationToken.None).ConfigureAwait(false);
            return exitCode == 0 && File.Exists(outputFile) ? outputFile : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the <c>#include &lt;...&gt;</c> lines at the start of
    /// <paramref name="sourceCode"/>, skipping blank lines and line comments, or an
    /// empty string.
    /// </summary>
    /// <param name="sourceCode">The HIP source to compile.</param>
    private static string GetLeadingIncludes(string sourceCode)
    {
        var builder = new StringBuilder();
        foreach (var line in sourceCode.AsSpan().EnumerateLines())
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#include <", StringComparison.Ordinal) &&
                trimmed.EndsWith(">", StringComparison.Ordinal))
            {
                builder.Append(trimmed).Append('\n');
            }
            else if (!trimmed.IsEmpty &&
                !trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                break;
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Returns true if <paramref name="stderr"/> reports a precompiled header that
    /// does not match the compilation, such as a different target or macro set.
    /// </summary>
    /// <param name="stderr">The error output of hipcc.</param>
    private static bool IsPrecompiledHeaderError(string stderr) =>
        stderr.Contains("AST file", StringComparison.Ordinal) ||
        stderr.Contains("PCH file", StringComparison.Ordinal);

    /// <summary>
    /// Creates the directory for the precompiled headers of this process and deletes
    /// it when the process exits.
    /// </summary>
    private static string CreatePrecompiledHeaderDirectory()
    {
        var directory = CreateTempDirectory();
        AppDomain.CurrentDomain.ProcessExit +=
            (_, _) => CleanupTempDirectory(directory);
        return directory;
    }
}
