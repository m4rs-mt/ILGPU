// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Compiler wrapper for Apple Metal, using <c>xcrun metal</c> to compile Metal Shading
/// Language source to an intermediate <c>.air</c> file and then linking it into a
/// <c>.metallib</c> Metal library.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="MetalCompiler"/> with a custom <c>xcrun</c> path and
/// SDK name.
/// </remarks>
/// <param name="xcrunPath">The absolute path to the <c>xcrun</c> tool.</param>
/// <param name="sdk">
/// The Apple SDK name to pass to <c>xcrun -sdk</c> (e.g. <c>macosx</c> or
/// <c>iphoneos</c>).
/// </param>
public sealed class MetalCompiler(
    string xcrunPath = MetalCompiler.DefaultXcrunPath,
    string sdk = MetalCompiler.DefaultSdk) : CompilerBase(xcrunPath)
{
    private const string DefaultXcrunPath = "/usr/bin/xcrun";
    private const string DefaultSdk = "macosx";

    /// <inheritdoc/>
    public override CompilationTarget Target => CompilationTarget.Metal;

    /// <inheritdoc/>
    protected override ToolchainComponent[] GetToolchainComponents()
    {
        return
        [
            CheckTool("xcrun", ExecutablePath, "--version"),
            CheckTool("metal", ExecutablePath, $"-sdk {sdk} metal --version"),
            CheckTool("metallib", ExecutablePath, $"-sdk {sdk} metallib --version"),
        ];
    }

    /// <summary>
    /// Compiles the Metal source code in <paramref name="request"/> in two steps:
    /// first to an intermediate <c>.air</c> file, then linked into a <c>.metallib</c>,
    /// which is returned as a base-64-encoded string on success.
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
            var inputFile = Path.Combine(tempDir, "input.metal");
            await File.WriteAllTextAsync(inputFile, request.SourceCode, ct)
                .ConfigureAwait(false);

            var airFile = Path.Combine(tempDir, "output.air");
            var includes = BuildIncludeArgs(request);
            var directives = BuildDirectiveArgs(request);

            // Step 1: .metal -> .air
            var compileArgs =
                $"-sdk {sdk} metal -c \"{inputFile}\" -o \"{airFile}\"" +
                $"{includes}{directives}";
            var (exitCode, stdout, stderr) = await RunProcessAsync(
                ExecutablePath,
                compileArgs,
                tempDir,
                ct).ConfigureAwait(false);

            if (exitCode != 0)
            {
                return new CompilationResult
                {
                    Success = false,
                    StdOut = stdout,
                    StdErr = stderr,
                    ExitCode = exitCode,
                    Target = CompilationTarget.Metal,
                    OutputType = request.OutputType,
                };
            }

            // Step 2: .air -> .metallib
            var metalLibFile = Path.Combine(tempDir, "output.metallib");
            var linkArgs = $"-sdk {sdk} metallib \"{airFile}\" -o \"{metalLibFile}\"";
            var (linkExitCode, linkStdout, linkStderr) = await RunProcessAsync(
                ExecutablePath,
                linkArgs,
                tempDir,
                ct).ConfigureAwait(false);

            var combinedStdout = stdout + linkStdout;
            var combinedStderr = stderr + linkStderr;

            if (linkExitCode != 0)
            {
                return new CompilationResult
                {
                    Success = false,
                    StdOut = combinedStdout,
                    StdErr = combinedStderr,
                    ExitCode = linkExitCode,
                    Target = CompilationTarget.Metal,
                    OutputType = request.OutputType,
                };
            }

            var output = Base64EncodeFile(metalLibFile);

            return new CompilationResult
            {
                Success = true,
                Output = output,
                StdOut = combinedStdout,
                StdErr = combinedStderr,
                ExitCode = 0,
                Target = CompilationTarget.Metal,
                OutputType = request.OutputType,
            };
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Returns the Metal compiler version by invoking
    /// <c>xcrun -sdk &lt;sdk&gt; metal --version</c>, or <c>null</c> if unavailable.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// The first line of the Metal compiler version output, or <c>null</c>.
    /// </returns>
    public override async Task<string?> GetVersionAsync(CancellationToken ct)
    {
        if (!IsAvailable)
            return null;

        try
        {
            var (_, stdout, _) = await RunProcessAsync(
                ExecutablePath,
                $"-sdk {sdk} metal --version",
                workingDirectory: null,
                ct).ConfigureAwait(false);
            return stdout.Trim().Split('\n')[0];
        }
        catch
        {
            return null;
        }
    }
}
